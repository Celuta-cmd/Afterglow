#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <algorithm>
#include <cmath>
#include <cstring>
#include <d2d1.h>
#include <d3d11.h>
#include <dcomp.h>
#include <dxgi1_2.h>
#include <memory>
#include <string>
#include <vector>
#include <windows.h>
#include <wrl/client.h>
using Microsoft::WRL::ComPtr;
static void Check(HRESULT hr) {
  if (FAILED(hr))
    throw hr;
}
static LARGE_INTEGER Now() {
  LARGE_INTEGER value;
  QueryPerformanceCounter(&value);
  return value;
}
struct Image {
  UINT width = 0, height = 0;
  ComPtr<ID3D11Texture2D> texture;
  ComPtr<IDCompositionSurface> surface;
};
struct Layer {
  HWND window = nullptr;
  UINT width = 0, height = 0;
  ComPtr<IDCompositionTarget> target;
  ComPtr<IDCompositionVisual> root, oldVisual, newVisual;
  ComPtr<IDCompositionEffectGroup> opacity;
};
struct ProbeResult {
  int frames = 0, errors = 0, reversals = 0;
  double meanMs = 0, p95Ms = 0, maxMs = 0, refreshHz = 0;
};
// One retained visual per phrase. Shared phrases are never crossfaded against
// a second copy of themselves. All changes are submitted in one transaction.
struct LyricInput {
  int id, width, height, offset;
  double y, opacity;
};
struct LyricGlyph {
  int id;
  Image image;
  ComPtr<IDCompositionVisual> visual;
  ComPtr<IDCompositionEffectGroup> effect;
  double fromY = 0, toY = 0, fromOpacity = 0, toOpacity = 0;
  LARGE_INTEGER started{};
  double seconds = 0;
  bool changed = false;
};
struct Engine {
  std::wstring probeDescription;

  ComPtr<ID3D11Device> device;
  ComPtr<ID3D11DeviceContext> context;
  ComPtr<IDCompositionDevice> composition;
  ComPtr<ID2D1Factory> factory;
  std::vector<Layer> layers;
  Image oldImage, newImage;
  LARGE_INTEGER start{}, frequency{};
  double duration = 2;
  bool active = false;
  bool lyricsMode = false;
  std::vector<LyricGlyph> glyphs;
  int lyricUploads = 0, lyricCommits = 0;
  ComPtr<IDXGIOutputDuplication> duplication;
  ComPtr<ID3D11Texture2D> probePixel;
  UINT probeX = 0, probeY = 0;
  double probeRefresh = 0;
  Engine() {
    QueryPerformanceFrequency(&frequency);
    Check(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr,
                            D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr, 0,
                            D3D11_SDK_VERSION, &device, nullptr, &context));
    ComPtr<IDXGIDevice> dxgi;
    Check(device.As(&dxgi));
    Check(DCompositionCreateDevice(dxgi.Get(), IID_PPV_ARGS(&composition)));
    Check(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED,
                            factory.GetAddressOf()));
  }
  double Amount(LONGLONG time = 0) const {
    if (!active)
      return 1;
    double t =
        std::clamp(double((time ? time : Now().QuadPart) - start.QuadPart) /
                       double(frequency.QuadPart) / duration,
                   0.0, 1.0);
    return t * t * (3 - 2 * t);
  }
  ComPtr<ID2D1RenderTarget> Target(IDXGISurface *surface) {
    ComPtr<ID2D1RenderTarget> result;
    auto properties = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_DEFAULT,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM,
                          D2D1_ALPHA_MODE_PREMULTIPLIED));
    Check(factory->CreateDxgiSurfaceRenderTarget(surface, properties, &result));
    return result;
  }
  ComPtr<ID2D1Bitmap> Bitmap(ID2D1RenderTarget *target, const Image &image) {
    ComPtr<IDXGISurface> surface;
    Check(image.texture.As(&surface));
    ComPtr<ID2D1Bitmap> bitmap;
    auto properties = D2D1::BitmapProperties(D2D1::PixelFormat(
        DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED));
    Check(target->CreateSharedBitmap(__uuidof(IDXGISurface), surface.Get(),
                                     &properties, &bitmap));
    return bitmap;
  }
  Image Texture(UINT width, UINT height, const BYTE *bytes = nullptr) {
    Image image;
    image.width = width;
    image.height = height;
    D3D11_TEXTURE2D_DESC desc{};
    desc.Width = width;
    desc.Height = height;
    desc.MipLevels = desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
    D3D11_SUBRESOURCE_DATA data{bytes, width * 4, 0};
    Check(device->CreateTexture2D(&desc, bytes ? &data : nullptr,
                                  &image.texture));
    return image;
  }
  void Compose(Image &result, double amount) {
    ComPtr<IDXGISurface> surface;
    Check(result.texture.As(&surface));
    auto target = Target(surface.Get());
    auto a = Bitmap(target.Get(), oldImage);
    auto b = Bitmap(target.Get(), newImage);
    target->BeginDraw();
    target->Clear(D2D1::ColorF(0, 0.0f));
    auto bounds = D2D1::RectF(0, 0, float(result.width), float(result.height));
    target->DrawBitmap(a.Get(), bounds, 1,
                       D2D1_BITMAP_INTERPOLATION_MODE_LINEAR);
    target->DrawBitmap(b.Get(), bounds, float(amount),
                       D2D1_BITMAP_INTERPOLATION_MODE_LINEAR);
    Check(target->EndDraw());
  }
  void PublishImage(Image &image) {
    Check(composition->CreateSurface(
        image.width, image.height, DXGI_FORMAT_B8G8R8A8_UNORM,
        DXGI_ALPHA_MODE_PREMULTIPLIED, &image.surface));
    ComPtr<IDXGISurface> surface;
    POINT offset{};
    Check(image.surface->BeginDraw(nullptr, IID_PPV_ARGS(&surface), &offset));
    try {
      auto target = Target(surface.Get());
      auto bitmap = Bitmap(target.Get(), image);
      target->BeginDraw();
      target->PushAxisAlignedClip(D2D1::RectF(float(offset.x), float(offset.y), float(offset.x + image.width), float(offset.y + image.height)), D2D1_ANTIALIAS_MODE_ALIASED);
      target->Clear(D2D1::ColorF(0, 0.0f));
      target->DrawBitmap(bitmap.Get(),
                         D2D1::RectF(float(offset.x), float(offset.y),
                                     float(offset.x + image.width),
                                     float(offset.y + image.height)));
      target->PopAxisAlignedClip();
      Check(target->EndDraw());
    } catch (...) {
      image.surface->EndDraw();
      throw;
    }
    Check(image.surface->EndDraw());
  }
  ComPtr<IDCompositionVisual> Visual(const Image &image, const Layer &layer) {
    ComPtr<IDCompositionVisual> visual;
    Check(composition->CreateVisual(&visual));
    Check(visual->SetContent(image.surface.Get()));
    float fit = std::max(float(layer.width) / image.width,
                         float(layer.height) / image.height);
    ComPtr<IDCompositionScaleTransform> scale;
    Check(composition->CreateScaleTransform(&scale));
    Check(scale->SetScaleX(fit));
    Check(scale->SetScaleY(fit));
    Check(visual->SetTransform(scale.Get()));
    Check(visual->SetOffsetX((float(layer.width) - image.width * fit) / 2));
    Check(visual->SetOffsetY((float(layer.height) - image.height * fit) / 2));
    return visual;
  }
  void Replace(const Image &image) {
    for (auto &layer : layers) {
      layer.oldVisual = Visual(image, layer);
      Check(layer.root->RemoveAllVisuals());
      Check(layer.root->AddVisual(layer.oldVisual.Get(), FALSE, nullptr));
      layer.newVisual.Reset();
      layer.opacity.Reset();
    }
    Check(composition->Commit());
  }
  void Initial(const BYTE *bytes, UINT width, UINT height) {
    oldImage = Texture(width, height, bytes);
    PublishImage(oldImage);
    Replace(oldImage);
    Check(composition->WaitForCommitCompletion());
  }
  ComPtr<IDCompositionAnimation> Motion(double from, double to, double seconds, LARGE_INTEGER begin) {
    ComPtr<IDCompositionAnimation> animation;
    Check(composition->CreateAnimation(&animation));
    auto delta = to - from;
    Check(animation->AddCubic(0, float(from), 0, float(3 * delta / (seconds * seconds)), float(-2 * delta / (seconds * seconds * seconds))));
    Check(animation->End(seconds, float(to)));
    Check(animation->SetAbsoluteBeginTime(begin));
    return animation;
  }
  double GlyphAmount(const LyricGlyph &glyph, LONGLONG time) const {
    if (glyph.seconds <= 0) return 1;
    auto t = std::clamp(double(time - glyph.started.QuadPart) / double(frequency.QuadPart) / glyph.seconds, 0.0, 1.0);
    return t * t * (3 - 2 * t);
  }
  void Lyrics(const LyricInput *input, int count, const BYTE *bytes, double seconds, bool reset) {
    if (layers.size() != 1 || count < 0 || count > 3) throw E_INVALIDARG;
    auto &layer = layers[0];
    if (reset || !lyricsMode) {
      Check(layer.root->RemoveAllVisuals());
      Check(layer.root->SetEffect(nullptr)); Check(layer.root->SetOffsetY(0.0f));
      glyphs.clear(); active = false; oldImage = {}; newImage = {};
    }
    lyricsMode = true;
    // Capture the live interpolation before replacing it, including a seek
    // during a running move. Raster images are reused until the phrase leaves.
    const auto begin = Now();
    for (auto it = glyphs.begin(); it != glyphs.end();) {
      auto amount = GlyphAmount(*it, begin.QuadPart);
      double opacity = it->fromOpacity + (it->toOpacity - it->fromOpacity) * amount;
      bool wanted = false;
      for (int i = 0; i < count; i++) if (input[i].id == it->id) wanted = true;
      if (!wanted && opacity < .001) { Check(layer.root->RemoveVisual(it->visual.Get())); it = glyphs.erase(it); }
      else ++it;
    }
    double shift = 0; bool matched = false;
    for (auto &glyph : glyphs) {
      auto amount = GlyphAmount(glyph, begin.QuadPart);
      for (int i = 0; i < count; i++) if (input[i].id == glyph.id) {
        if (!matched) { shift = input[i].y - (glyph.fromY + (glyph.toY - glyph.fromY) * amount); matched = true; }
      }
    }
    for (auto &glyph : glyphs) {
      const LyricInput *wanted = nullptr;
      for (int i = 0; i < count; i++) if (input[i].id == glyph.id) wanted = &input[i];
      glyph.changed = wanted != nullptr || glyph.toOpacity > 0;
      // A departing phrase keeps its original fade deadline during rapid
      // advances. New updates must not extend its lifetime indefinitely.
      if (!glyph.changed) continue;
      auto amount = GlyphAmount(glyph, begin.QuadPart);
      glyph.fromY += (glyph.toY - glyph.fromY) * amount;
      glyph.fromOpacity += (glyph.toOpacity - glyph.fromOpacity) * amount;
      glyph.toY = wanted ? wanted->y : glyph.fromY + shift;
      glyph.toOpacity = wanted ? wanted->opacity : 0;
      glyph.started = begin; glyph.seconds = seconds;
    }
    for (int i = 0; i < count; i++) {
      const auto &line = input[i];
      if (line.width <= 0 || line.height <= 0 || line.offset < 0) throw E_INVALIDARG;
      if (std::any_of(glyphs.begin(), glyphs.end(), [&](const LyricGlyph &g) { return g.id == line.id; })) continue;
      LyricGlyph glyph{}; glyph.id = line.id;
      glyph.image = Texture(UINT(line.width), UINT(line.height), bytes + line.offset); PublishImage(glyph.image);
      lyricUploads++;
      Check(composition->CreateVisual(&glyph.visual)); Check(glyph.visual->SetContent(glyph.image.surface.Get()));
      Check(composition->CreateEffectGroup(&glyph.effect)); Check(glyph.visual->SetEffect(glyph.effect.Get()));
      glyph.toY = line.y; glyph.fromY = line.y - shift;
      glyph.toOpacity = line.opacity; glyph.fromOpacity = seconds > 0 ? 0 : line.opacity;
      glyph.started = begin; glyph.seconds = seconds; glyph.changed = true;
      Check(layer.root->AddVisual(glyph.visual.Get(), TRUE, nullptr));
      glyphs.push_back(std::move(glyph));
    }
    duration = seconds; start = begin; active = seconds > 0;
    for (auto &glyph : glyphs) {
      if (!glyph.changed && active) continue;
      if (active) {
        auto move = Motion(glyph.fromY, glyph.toY, seconds, begin);
        auto fade = Motion(glyph.fromOpacity, glyph.toOpacity, seconds, begin);
        Check(glyph.visual->SetOffsetY(move.Get())); Check(glyph.effect->SetOpacity(fade.Get()));
      } else {
        Check(glyph.visual->SetOffsetY(float(glyph.toY))); Check(glyph.effect->SetOpacity(float(glyph.toOpacity)));
      }
    }
    // No intermediate commit may expose new glyphs with old positions/opacity.
    Check(composition->Commit());
    lyricCommits++;
    if (!active) FinishLyrics();
  }
  void FinishLyrics() {
    for (auto it = glyphs.begin(); it != glyphs.end();) {
      if (it->toOpacity == 0) { Check(layers[0].root->RemoveVisual(it->visual.Get())); it = glyphs.erase(it); }
      else {
        it->fromY = it->toY; it->fromOpacity = it->toOpacity;
        it->seconds = 0;
        Check(it->visual->SetOffsetY(float(it->toY))); Check(it->effect->SetOpacity(float(it->toOpacity))); ++it;
      }
    }
    active = false; Check(composition->Commit());
  }
  void ComposeLyrics(Image &result) {
    ComPtr<IDXGISurface> surface; Check(result.texture.As(&surface));
    auto target = Target(surface.Get()); target->BeginDraw(); target->Clear(D2D1::ColorF(0, 0.0f));
    auto time = Now().QuadPart;
    for (const auto &glyph : glyphs) {
      auto amount = GlyphAmount(glyph, time);
      auto bitmap = Bitmap(target.Get(), glyph.image);
      float y = float(glyph.fromY + (glyph.toY - glyph.fromY) * amount);
      float opacity = float(glyph.fromOpacity + (glyph.toOpacity - glyph.fromOpacity) * amount);
      target->DrawBitmap(bitmap.Get(), D2D1::RectF(0, y, float(glyph.image.width), y + glyph.image.height), opacity);
    }
    Check(target->EndDraw());
  }
  void Begin(const BYTE *bytes, UINT width, UINT height, double seconds) {
    newImage = Texture(width, height, bytes);
    PublishImage(newImage);
    duration = seconds;
    ComPtr<IDCompositionAnimation> animation;
    Check(composition->CreateAnimation(&animation));
    Check(animation->AddCubic(0, 0, 0, float(3 / (seconds * seconds)),
                              float(-2 / (seconds * seconds * seconds))));
    Check(animation->End(seconds, 1));
    for (auto &layer : layers) {
      layer.newVisual = Visual(newImage, layer);
      Check(composition->CreateEffectGroup(&layer.opacity));
      Check(layer.opacity->SetOpacity(animation.Get()));
      Check(layer.newVisual->SetEffect(layer.opacity.Get()));
      Check(layer.root->AddVisual(layer.newVisual.Get(), TRUE,
                                  layer.oldVisual.Get()));
    }
    start = Now();
    Check(animation->SetAbsoluteBeginTime(start));
    Check(composition->Commit());
    active = true;
  }
  void Finish() {
    if (lyricsMode) { FinishLyrics(); return; }
    for (auto &layer : layers) {
      Check(layer.opacity->SetOpacity(1.0f));
      Check(layer.root->RemoveVisual(layer.oldVisual.Get()));
      layer.oldVisual = layer.newVisual;
      layer.newVisual.Reset();
      layer.opacity.Reset();
    }
    Check(composition->Commit());
    oldImage = std::move(newImage);
    active = false;
  }
  double Freeze() {
    if (lyricsMode) return Amount();
    if (!active)
      return 1;
    double amount = Amount();
    Image held = Texture(newImage.width, newImage.height);
    Compose(held, amount);
    PublishImage(held);
    Replace(held);
    oldImage = std::move(held);
    newImage = Image{};
    active = false;
    return amount;
  }
  void ReadTexture(ID3D11Texture2D *texture, BYTE *bytes, UINT width,
                   UINT height) {
    D3D11_TEXTURE2D_DESC desc{};
    texture->GetDesc(&desc);
    desc.Usage = D3D11_USAGE_STAGING;
    desc.BindFlags = desc.MiscFlags = 0;
    desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    ComPtr<ID3D11Texture2D> staging;
    Check(device->CreateTexture2D(&desc, nullptr, &staging));
    context->CopyResource(staging.Get(), texture);
    D3D11_MAPPED_SUBRESOURCE map{};
    Check(context->Map(staging.Get(), 0, D3D11_MAP_READ, 0, &map));
    for (UINT y = 0; y < height; y++)
      std::memcpy(bytes + y * width * 4,
                  static_cast<BYTE *>(map.pData) + y * map.RowPitch, width * 4);
    context->Unmap(staging.Get(), 0);
  }
  void Capture(BYTE *bytes, UINT width, UINT height, double &amount) {
    amount = Amount();
    Image result = Texture(width, height);
    if (lyricsMode)
      ComposeLyrics(result);
    else if (active)
      Compose(result, amount);
    else {
      ComPtr<IDXGISurface> surface;
      Check(result.texture.As(&surface));
      auto target = Target(surface.Get());
      auto image = Bitmap(target.Get(), oldImage);
      target->BeginDraw();
      target->Clear(D2D1::ColorF(0, 0.0f));
      target->DrawBitmap(image.Get(),
                         D2D1::RectF(0, 0, float(width), float(height)));
      Check(target->EndDraw());
    }
    ReadTexture(result.texture.Get(), bytes, width, height);
  }
  bool ProbeBegin() {
    probeDescription.clear();
    duplication.Reset();
    probePixel.Reset();
    ComPtr<IDXGIDevice> dxgi;
    Check(device.As(&dxgi));
    ComPtr<IDXGIAdapter> adapter;
    Check(dxgi->GetAdapter(&adapter));
    for (UINT index = 0;; index++) {
      ComPtr<IDXGIOutput> output;
      if (adapter->EnumOutputs(index, &output) == DXGI_ERROR_NOT_FOUND)
        break;
      DXGI_OUTPUT_DESC desc{};
      Check(output->GetDesc(&desc));
      if (!desc.AttachedToDesktop)
        continue;
      ComPtr<IDXGIOutput1> output1;
      Check(output.As(&output1));
      ComPtr<IDXGIOutputDuplication> duplicate;
      HRESULT duplicateStatus =
          output1->DuplicateOutput(device.Get(), &duplicate);
      probeDescription += L"Output " + std::to_wstring(index) +
                          L" duplication HRESULT " +
                          std::to_wstring(duplicateStatus) + L"; ";
      if (FAILED(duplicateStatus))
        continue;
      DXGI_OUTDUPL_FRAME_INFO frame{};
      ComPtr<IDXGIResource> resource;
      HRESULT acquireStatus =
          duplicate->AcquireNextFrame(1000, &frame, &resource);
      probeDescription +=
          L"acquire HRESULT " + std::to_wstring(acquireStatus) + L"; ";
      if (FAILED(acquireStatus))
        continue;
      // A duplication stream can first report cursor metadata with no desktop
      // image. Force an exposed edge update, then wait for a real presentation.
      for (int retry = 0; frame.LastPresentTime.QuadPart == 0 && retry < 4;
           retry++) {
        Check(duplicate->ReleaseFrame());
        resource.Reset();
        for (auto &layer : layers)
          Check(layer.root->SetOffsetX(retry % 2 ? 0.0f : 1.0f));
        Check(composition->Commit());
        acquireStatus = duplicate->AcquireNextFrame(1000, &frame, &resource);
        if (FAILED(acquireStatus))
          break;
      }
      for (auto &layer : layers)
        Check(layer.root->SetOffsetX(0.0f));
      Check(composition->Commit());
      if (FAILED(acquireStatus))
        continue;
      bool found = false;
      try {
        ComPtr<ID3D11Texture2D> texture;
        Check(resource.As(&texture));
        D3D11_TEXTURE2D_DESC dimensions{};
        texture->GetDesc(&dimensions);
        probeDescription +=
            L"size " + std::to_wstring(dimensions.Width) + L"x" +
            std::to_wstring(dimensions.Height) + L" protected " +
            std::to_wstring(frame.ProtectedContentMaskedOut) + L" timestamp " +
            std::to_wstring(frame.LastPresentTime.QuadPart) + L"; ";
        std::vector<BYTE> pixels(size_t(dimensions.Width) * dimensions.Height *
                                 4);
        ReadTexture(texture.Get(), pixels.data(), dimensions.Width,
                    dimensions.Height);
        auto center =
            &pixels[(size_t(dimensions.Height / 2) * dimensions.Width +
                     dimensions.Width / 2) *
                    4];
        probeDescription += L"center BGR " + std::to_wstring(center[0]) + L"," +
                            std::to_wstring(center[1]) + L"," +
                            std::to_wstring(center[2]) + L"; ";
        int closest = 1000;
        for (UINT y = 80; y + 80 < dimensions.Height && !found; y += 24)
          for (UINT x = 80; x + 80 < dimensions.Width; x += 24) {
            auto p = &pixels[(size_t(y) * dimensions.Width + x) * 4];
            closest = std::min(closest, std::abs(int(p[0]) - 70) +
                                            std::abs(int(p[1]) - 35) +
                                            std::abs(int(p[2]) - 210));
            if (std::abs(int(p[0]) - 70) < 3 && std::abs(int(p[1]) - 35) < 3 &&
                std::abs(int(p[2]) - 210) < 3) {
              probeX = x;
              probeY = y;
              found = true;
              break;
            }
          }
        probeDescription +=
            L"closest fixture color error " + std::to_wstring(closest) + L"; ";
      } catch (...) {
        duplicate->ReleaseFrame();
        throw;
      }
      Check(duplicate->ReleaseFrame());
      if (found) {
        duplication = duplicate;
        probePixel = Texture(1, 1).texture;
        DXGI_MODE_DESC mode{};
        mode.Width =
            UINT(desc.DesktopCoordinates.right - desc.DesktopCoordinates.left);
        mode.Height =
            UINT(desc.DesktopCoordinates.bottom - desc.DesktopCoordinates.top);
        mode.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        DXGI_MODE_DESC match{};
        if (SUCCEEDED(
                output->FindClosestMatchingMode(&mode, &match, device.Get())) &&
            match.RefreshRate.Denominator)
          probeRefresh = double(match.RefreshRate.Numerator) /
                         match.RefreshRate.Denominator;
        DEVMODEW currentMode{};
        currentMode.dmSize = sizeof(currentMode);
        if (EnumDisplaySettingsExW(desc.DeviceName, ENUM_CURRENT_SETTINGS,
                                   &currentMode, 0) &&
            currentMode.dmDisplayFrequency > 1)
          probeRefresh = currentMode.dmDisplayFrequency;
        return true;
      }
    }
    return false;
  }
  ProbeResult ProbeRun() {
    ProbeResult result;
    result.refreshHz = probeRefresh;
    if (!duplication)
      return result;
    std::vector<double> intervals;
    LONGLONG previous = 0;
    int lastBlue = 70;
    while (double(Now().QuadPart - start.QuadPart) / frequency.QuadPart <
           duration + .12) {
      DXGI_OUTDUPL_FRAME_INFO frame{};
      ComPtr<IDXGIResource> resource;
      HRESULT hr = duplication->AcquireNextFrame(150, &frame, &resource);
      if (hr == DXGI_ERROR_WAIT_TIMEOUT)
        continue;
      Check(hr);
      try {
        if (frame.LastPresentTime.QuadPart >= start.QuadPart &&
            frame.LastPresentTime.QuadPart > previous) {
          ComPtr<ID3D11Texture2D> texture;
          Check(resource.As(&texture));
          D3D11_BOX box{probeX, probeY, 0, probeX + 1, probeY + 1, 1};
          context->CopySubresourceRegion(probePixel.Get(), 0, 0, 0, 0,
                                         texture.Get(), 0, &box);
          BYTE pixel[4];
          ReadTexture(probePixel.Get(), pixel, 1, 1);
          double amount = Amount(frame.LastPresentTime.QuadPart);
          double expected[]{70 + 155 * amount, 35 + 75 * amount,
                            210 - 170 * amount};
          for (int i = 0; i < 3; i++)
            if (std::abs(pixel[i] - expected[i]) > 18) {
              result.errors++;
              break;
            }
          if (pixel[0] < lastBlue - 3)
            result.reversals++;
          lastBlue = pixel[0];
          if (previous)
            intervals.push_back(
                double(frame.LastPresentTime.QuadPart - previous) * 1000 /
                frequency.QuadPart);
          previous = frame.LastPresentTime.QuadPart;
          result.frames++;
        }
      } catch (...) {
        duplication->ReleaseFrame();
        throw;
      }
      Check(duplication->ReleaseFrame());
    }
    duplication.Reset();
    probePixel.Reset();
    if (!intervals.empty()) {
      for (double ms : intervals)
        result.meanMs += ms;
      result.meanMs /= intervals.size();
      std::sort(intervals.begin(), intervals.end());
      result.maxMs = intervals.back();
      result.p95Ms = intervals[std::min(
          intervals.size() - 1, size_t(std::ceil(intervals.size() * .95)) - 1)];
    }
    return result;
  }
};
#define EXPORT extern "C" __declspec(dllexport)
EXPORT HRESULT AG_Create(HWND *windows, int *widths, int *heights, int count,
                         Engine **result) {
  if (!result || count < 1)
    return E_INVALIDARG;
  *result = nullptr;
  try {
    auto engine = std::make_unique<Engine>();
    for (int i = 0; i < count; i++) {
      Layer layer;
      layer.window = windows[i];
      layer.width = UINT(widths[i]);
      layer.height = UINT(heights[i]);
      Check(engine->composition->CreateTargetForHwnd(layer.window, TRUE,
                                                     &layer.target));
      Check(engine->composition->CreateVisual(&layer.root));
      Check(layer.root->SetClip(
          D2D1::RectF(0, 0, float(layer.width), float(layer.height))));
      Check(layer.target->SetRoot(layer.root.Get()));
      engine->layers.push_back(std::move(layer));
    }
    *result = engine.release();
    return S_OK;
  } catch (HRESULT hr) {
    return hr;
  } catch (...) {
    return E_FAIL;
  }
}
EXPORT HRESULT AG_Initial(Engine *engine, const BYTE *bytes, int width,
                          int height) {
  try {
    engine->Initial(bytes, UINT(width), UINT(height));
    return S_OK;
  } catch (HRESULT hr) {
    return hr;
  } catch (...) {
    return E_FAIL;
  }
}
EXPORT HRESULT AG_Lyrics(Engine *engine, const LyricInput *input, int count, const BYTE *bytes, double seconds, int reset) {
  try { engine->Lyrics(input, count, bytes, seconds, reset != 0); return S_OK; }
  catch (HRESULT hr) { return hr; } catch (...) { return E_FAIL; }
}
EXPORT HRESULT AG_LyricState(Engine *engine, int id, double *y, double *opacity, int *count, int *uploads, int *commits) {
  *count = int(engine->glyphs.size()); *uploads = engine->lyricUploads; *commits = engine->lyricCommits;
  auto time = Now().QuadPart;
  for (const auto &glyph : engine->glyphs) if (glyph.id == id) {
    auto amount = engine->GlyphAmount(glyph, time);
    *y = glyph.fromY + (glyph.toY - glyph.fromY) * amount;
    *opacity = glyph.fromOpacity + (glyph.toOpacity - glyph.fromOpacity) * amount;
    return S_OK;
  }
  return E_INVALIDARG;
}
EXPORT HRESULT AG_Begin(Engine *engine, const BYTE *bytes, int width,
                        int height, double duration) {
  try {
    engine->Begin(bytes, UINT(width), UINT(height), duration);
    return S_OK;
  } catch (HRESULT hr) {
    return hr;
  } catch (...) {
    return E_FAIL;
  }
}
EXPORT HRESULT AG_Finish(Engine *engine) {
  try {
    engine->Finish();
    return S_OK;
  } catch (HRESULT hr) {
    return hr;
  } catch (...) {
    return E_FAIL;
  }
}
EXPORT HRESULT AG_Freeze(Engine *engine, double *amount) {
  try {
    *amount = engine->Freeze();
    return S_OK;
  } catch (HRESULT hr) {
    return hr;
  } catch (...) {
    return E_FAIL;
  }
}
EXPORT double AG_Progress(Engine *engine) { return engine->Amount(); }
EXPORT HRESULT AG_Capture(Engine *engine, BYTE *bytes, int width, int height,
                          double *amount) {
  try {
    engine->Capture(bytes, UINT(width), UINT(height), *amount);
    return S_OK;
  } catch (HRESULT hr) {
    return hr;
  } catch (...) {
    return E_FAIL;
  }
}
EXPORT HRESULT AG_ProbeBegin(Engine *engine, int *available) {
  try {
    *available = engine->ProbeBegin() ? 1 : 0;
    return S_OK;
  } catch (HRESULT hr) {
    return hr;
  } catch (...) {
    return E_FAIL;
  }
}
EXPORT HRESULT AG_ProbeRun(Engine *engine, ProbeResult *result) {
  try {
    *result = engine->ProbeRun();
    return S_OK;
  } catch (HRESULT hr) {
    return hr;
  } catch (...) {
    return E_FAIL;
  }
}
EXPORT void AG_Destroy(Engine *engine) { delete engine; }
EXPORT int AG_ProbeDescription(Engine *engine, wchar_t *text, int capacity) {
  if (capacity < 1)
    return 0;
  int count = std::min(int(engine->probeDescription.size()), capacity - 1);
  std::memcpy(text, engine->probeDescription.data(),
              size_t(count) * sizeof(wchar_t));
  text[count] = 0;
  return count;
}
