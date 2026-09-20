// Ported from src/shaders/lib/noise.glsl.js. Preserve the source equations.

#ifndef NOISE_LIB_INCLUDED
#define NOISE_LIB_INCLUDED

float3 mod289v3(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 mod289v4(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 permute289(float4 x) { return mod289v4(((x * 34.0) + 1.0) * x); }
float4 taylorInvSqrt4(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

float hash11(float p) {
  p = frac(p * 0.1031);
  p *= p + 33.33;
  p *= p + p;
  return frac(p);
}

float2 hash21(float p) {
  float3 p3 = frac(((float3)p) * float3(0.1031, 0.1030, 0.0973));
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.xx + p3.yz) * p3.zy);
}

float3 hash31(float p) {
  float3 p3 = frac(((float3)p) * float3(0.1031, 0.1030, 0.0973));
  p3 += dot(p3, p3.yxz + 33.33);
  return frac((p3.xxy + p3.yzz) * p3.zyx);
}

float hash13(float3 p3) {
  p3 = frac(p3 * 0.1031);
  p3 += dot(p3, p3.zyx + 31.32);
  return frac((p3.x + p3.y) * p3.z);
}

/* ---- Ashima / Stefan Gustavson simplex noise ---- */
float snoise(float3 v) {
  const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
  const float4 D = float4(0.0, 0.5, 1.0, 2.0);

  float3 i  = floor(v + dot(v, C.yyy));
  float3 x0 = v - i + dot(i, C.xxx);

  float3 g = step(x0.yzx, x0.xyz);
  float3 l = 1.0 - g;
  float3 i1 = min(g.xyz, l.zxy);
  float3 i2 = max(g.xyz, l.zxy);

  float3 x1 = x0 - i1 + C.xxx;
  float3 x2 = x0 - i2 + C.yyy;
  float3 x3 = x0 - D.yyy;

  i = mod289v3(i);
  float4 p = permute289(permute289(permute289(
             i.z + float4(0.0, i1.z, i2.z, 1.0))
           + i.y + float4(0.0, i1.y, i2.y, 1.0))
           + i.x + float4(0.0, i1.x, i2.x, 1.0));

  float n_ = 0.142857142857;
  float3 ns = n_ * D.wyz - D.xzx;

  float4 j = p - 49.0 * floor(p * ns.z * ns.z);

  float4 x_ = floor(j * ns.z);
  float4 y_ = floor(j - 7.0 * x_);

  float4 x = x_ * ns.x + ns.yyyy;
  float4 y = y_ * ns.x + ns.yyyy;
  float4 h = 1.0 - abs(x) - abs(y);

  float4 b0 = float4(x.xy, y.xy);
  float4 b1 = float4(x.zw, y.zw);

  float4 s0 = floor(b0) * 2.0 + 1.0;
  float4 s1 = floor(b1) * 2.0 + 1.0;
  float4 sh = -step(h, ((float4)0.0));

  float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
  float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

  float3 p0 = float3(a0.xy, h.x);
  float3 p1 = float3(a0.zw, h.y);
  float3 p2 = float3(a1.xy, h.z);
  float3 p3 = float3(a1.zw, h.w);

  float4 norm = taylorInvSqrt4(float4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
  p0 *= norm.x; p1 *= norm.y; p2 *= norm.z; p3 *= norm.w;

  float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
  m = m * m;
  return 42.0 * dot(m * m, float4(dot(p0, x0), dot(p1, x1), dot(p2, x2), dot(p3, x3)));
}

/** snoise remapped to 0..1 */
float snoise01(float3 p) { return snoise(p) * 0.5 + 0.5; }

float fbm3(float3 p) {
  float v = 0.0;
  float a = 0.5;
  for (int i = 0; i < 3; i++) {
    v += a * snoise(p);
    p *= 2.02;
    a *= 0.5;
  }
  return v;
}

float fbm4(float3 p) {
  float v = 0.0;
  float a = 0.5;
  for (int i = 0; i < 4; i++) {
    v += a * snoise(p);
    p = p * 2.03 + float3(17.3, 5.1, 9.7);
    a *= 0.5;
  }
  return v;
}

/** Ridged multifractal — sharp filaments, ideal for flames and cracks. */
float ridged(float3 p, int unusedOctaves) {
  float v = 0.0;
  float a = 0.5;
  for (int i = 0; i < 4; i++) {
    v += a * (1.0 - abs(snoise(p)));
    p *= 2.06;
    a *= 0.5;
  }
  return v;
}

/** Divergence-free curl noise — used to swirl particles and flames. */
float3 curlNoise(float3 p) {
  const float e = 0.12;
  float3 dx = float3(e, 0.0, 0.0);
  float3 dy = float3(0.0, e, 0.0);
  float3 dz = float3(0.0, 0.0, e);

  float x0 = snoise(p - dx), x1 = snoise(p + dx);
  float y0 = snoise(p - dy), y1 = snoise(p + dy);
  float z0 = snoise(p - dz), z1 = snoise(p + dz);

  float3 pb = p + float3(31.416, 47.853, 12.793);
  float bx0 = snoise(pb - dx), bx1 = snoise(pb + dx);
  float by0 = snoise(pb - dy), by1 = snoise(pb + dy);
  float bz0 = snoise(pb - dz), bz1 = snoise(pb + dz);

  float inv = 1.0 / (2.0 * e);
  float3 grad1 = float3(x1 - x0, y1 - y0, z1 - z0) * inv;
  float3 grad2 = float3(bx1 - bx0, by1 - by0, bz1 - bz0) * inv;
  return normalize(cross(grad1, grad2) + 1e-5);
}

/** Cheap 2D voronoi. Returns x = distance to closest cell, y = cell id hash. */
float2 voronoi2(float2 p) {
  float2 n = floor(p);
  float2 f = frac(p);
  float minDist = 8.0;
  float id = 0.0;
  for (int j = -1; j <= 1; j++) {
    for (int i = -1; i <= 1; i++) {
      float2 g = float2(float(i), float(j));
      float2 o = hash21(dot(n + g, float2(7.13, 113.17)));
      float2 r = g + o - f;
      float d = dot(r, r);
      if (d < minDist) { minDist = d; id = hash11(dot(n + g, float2(31.7, 57.1))); }
    }
  }
  return float2(sqrt(minDist), id);
}

float2x2 rot2(float a) {
  float s = sin(a), c = cos(a);
  return float2x2(c, -s, s, c);
}

#endif
