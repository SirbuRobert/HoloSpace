using UnityEngine;

/// <summary>
/// Generator procedural de texturi pentru planete: Perlin noise + palete de culori.
/// Nu are nevoie de fisiere externe, totul e calculat la runtime.
///
/// Stiluri:
///   Banded  - benzi orizontale cu margini ondulate (Jupiter, Saturn)
///   Mottled - pete neregulate pe suprafata (Mercur, Marte)
///   Smooth  - gradient subtil cu variatii mici (Uranus, Neptun)
///   Swirl   - vartejuri de nori (Venus)
///   Earth   - continente verzi/brune pe ocean albastru
///   Sun     - suprafata incandescenta cu pete solare
/// </summary>
public static class PlanetTextureGenerator
{
    public enum Style { Banded, Mottled, Smooth, Swirl, Earth, Sun }

    // ─── Profiluri per planeta ──────────────────────────────────────────────────

    public static Texture2D ForSun()     => Generate(Style.Sun,     512, 256,
        new Color(1.00f, 0.85f, 0.10f),
        new Color(0.95f, 0.40f, 0.00f),
        new Color(1.00f, 0.65f, 0.05f), seed: 1);

    public static Texture2D ForMercury() => Generate(Style.Mottled, 512, 256,
        new Color(0.70f, 0.65f, 0.60f),
        new Color(0.45f, 0.42f, 0.40f),
        new Color(0.60f, 0.57f, 0.54f), seed: 2);

    public static Texture2D ForVenus()   => Generate(Style.Swirl,   512, 256,
        new Color(0.95f, 0.85f, 0.55f),
        new Color(0.80f, 0.65f, 0.30f),
        new Color(0.90f, 0.78f, 0.45f), seed: 3);

    public static Texture2D ForEarth()   => Generate(Style.Earth,   512, 256,
        new Color(0.10f, 0.40f, 0.85f),
        new Color(0.15f, 0.55f, 0.20f),
        new Color(0.65f, 0.55f, 0.35f), seed: 4);

    public static Texture2D ForMars()    => Generate(Style.Mottled, 512, 256,
        new Color(0.80f, 0.32f, 0.10f),
        new Color(0.55f, 0.18f, 0.05f),
        new Color(0.70f, 0.45f, 0.25f), seed: 5);

    public static Texture2D ForJupiter() => Generate(Style.Banded,  512, 256,
        new Color(0.90f, 0.72f, 0.50f),
        new Color(0.70f, 0.38f, 0.18f),
        new Color(0.98f, 0.92f, 0.80f), seed: 6);

    public static Texture2D ForSaturn()  => Generate(Style.Banded,  512, 256,
        new Color(0.92f, 0.84f, 0.62f),
        new Color(0.75f, 0.60f, 0.35f),
        new Color(1.00f, 0.96f, 0.82f), seed: 7);

    public static Texture2D ForUranus()  => Generate(Style.Smooth,  512, 256,
        new Color(0.50f, 0.85f, 0.90f),
        new Color(0.30f, 0.68f, 0.78f),
        new Color(0.65f, 0.92f, 0.96f), seed: 8);

    public static Texture2D ForNeptune() => Generate(Style.Smooth,  512, 256,
        new Color(0.10f, 0.28f, 0.88f),
        new Color(0.05f, 0.15f, 0.70f),
        new Color(0.25f, 0.50f, 0.98f), seed: 9);

    // ─── Generator principal ────────────────────────────────────────────────────

    /// <summary>
    /// Genereaza o textura Texture2D echirectangulara (proiectata pe o sfera Unity).
    /// </summary>
    /// <param name="style">Stilul vizual al planetei.</param>
    /// <param name="width">Latimea texturii in pixeli.</param>
    /// <param name="height">Inaltimea texturii in pixeli.</param>
    /// <param name="colorA">Culoarea principala / ocean / baza.</param>
    /// <param name="colorB">Culoarea secundara / continente / benzi.</param>
    /// <param name="colorC">Culoarea tertiara / highlight / benzi deschise.</param>
    /// <param name="seed">Seed pentru offset Perlin (planete diferite dau suprafete diferite).</param>
    public static Texture2D Generate(Style style, int width, int height,
                                     Color colorA, Color colorB, Color colorC,
                                     int seed = 0)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, true);
        Color[] pixels = new Color[width * height];

        float ox = seed * 137.5f; // offset unic per planeta
        float oy = seed * 91.3f;

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / height; // [0, 1] sus-jos

            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width; // [0, 1] stanga-dreapta

                Color pixel = style switch
                {
                    Style.Banded  => SampleBanded (u, v, ox, oy, colorA, colorB, colorC),
                    Style.Mottled => SampleMottled(u, v, ox, oy, colorA, colorB, colorC),
                    Style.Smooth  => SampleSmooth (u, v, ox, oy, colorA, colorB, colorC),
                    Style.Swirl   => SampleSwirl  (u, v, ox, oy, colorA, colorB, colorC),
                    Style.Earth   => SampleEarth  (u, v, ox, oy, colorA, colorB, colorC),
                    Style.Sun     => SampleSun    (u, v, ox, oy, colorA, colorB, colorC),
                    _             => colorA
                };

                pixels[y * width + x] = pixel;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }

    // ─── Stiluri ────────────────────────────────────────────────────────────────

    // Jupiter / Saturn: benzi orizontale cu margini ondulate de Perlin noise
    private static Color SampleBanded(float u, float v, float ox, float oy,
                                      Color cA, Color cB, Color cC)
    {
        // Ondularea benzilor cu un camp de noise orizontal
        float warp  = Perlin(u * 3f + ox, v * 1.5f + oy) * 0.12f;
        float bands = Mathf.PerlinNoise(u * 0.5f + ox, (v + warp) * 8f + oy);

        // Banda importanta: Marea Pata Rosie pentru Jupiter (seed 6)
        float spot = 0f;
        if (ox > 800f && ox < 850f) // numai Jupiter (seed 6, ox ~= 823)
        {
            float dx = (u - 0.65f) * 3.5f;
            float dy = (v - 0.45f) * 6f;
            spot = Mathf.Max(0f, 1f - (dx * dx + dy * dy) * 8f);
        }

        // Zgomot fin pentru textura
        float detail = Perlin(u * 12f + ox, v * 12f + oy) * 0.15f;

        float t = Mathf.Clamp01(bands + detail);
        Color base_ = t < 0.5f ? Color.Lerp(cA, cB, t * 2f)
                                : Color.Lerp(cB, cC, (t - 0.5f) * 2f);

        return Color.Lerp(base_, new Color(0.85f, 0.25f, 0.10f), spot * 0.7f);
    }

    // Mercur / Marte: pete neregulate, teren accidentat
    private static Color SampleMottled(float u, float v, float ox, float oy,
                                       Color cA, Color cB, Color cC)
    {
        float n1 = Perlin(u * 4f  + ox, v * 4f  + oy);
        float n2 = Perlin(u * 8f  + ox, v * 8f  + oy) * 0.5f;
        float n3 = Perlin(u * 16f + ox, v * 16f + oy) * 0.25f;
        float n  = Mathf.Clamp01(n1 + n2 + n3);

        // Cratere: spot-uri intunecate mici
        float craters = 0f;
        for (int i = 0; i < 6; i++)
        {
            float cx = Mathf.Repeat(ox * 0.01f + i * 0.17f, 1f);
            float cy = Mathf.Repeat(oy * 0.01f + i * 0.23f, 1f);
            float dx = (u - cx) * 4f;
            float dy = (v - cy) * 4f;
            float r  = dx * dx + dy * dy;
            if (r < 0.04f) craters += (0.04f - r) * 12f;
        }
        craters = Mathf.Clamp01(craters);

        Color c = n < 0.4f ? Color.Lerp(cA, cB, n / 0.4f)
                : n < 0.7f ? Color.Lerp(cB, cC, (n - 0.4f) / 0.3f)
                           : cC;

        return Color.Lerp(c, cB * 0.6f, craters);
    }

    // Uranus / Neptun: gradient subtil, aspect neted
    private static Color SampleSmooth(float u, float v, float ox, float oy,
                                      Color cA, Color cB, Color cC)
    {
        float n1  = Perlin(u * 2f + ox, v * 2f + oy);
        float n2  = Perlin(u * 5f + ox, v * 5f + oy) * 0.3f;
        float pole = Mathf.Exp(-Mathf.Pow((v - 0.5f) * 2.5f, 2f)); // highlight la ecuator

        float t = Mathf.Clamp01(n1 * 0.6f + n2 + pole * 0.2f);
        return t < 0.5f ? Color.Lerp(cA, cB, t * 2f)
                        : Color.Lerp(cB, cC, (t - 0.5f) * 2f);
    }

    // Venus: vartejuri de nori galbui
    private static Color SampleSwirl(float u, float v, float ox, float oy,
                                     Color cA, Color cB, Color cC)
    {
        // Distorsionam coordonatele pentru efect de vartej
        float angle = Perlin(u * 3f + ox, v * 3f + oy) * Mathf.PI * 4f;
        float ru    = u + Mathf.Cos(angle) * 0.08f;
        float rv    = v + Mathf.Sin(angle) * 0.08f;

        float n1 = Perlin(ru * 5f + ox, rv * 5f + oy);
        float n2 = Perlin(ru * 10f + ox, rv * 10f + oy) * 0.4f;
        float t  = Mathf.Clamp01(n1 + n2);

        return t < 0.5f ? Color.Lerp(cA, cB, t * 2f)
                        : Color.Lerp(cB, cC, (t - 0.5f) * 2f);
    }

    // Pamant: ocean albastru + continente verzi/brune + calote polare
    private static Color SampleEarth(float u, float v, float ox, float oy,
                                     Color ocean, Color land, Color desert)
    {
        float n1 = Perlin(u * 3f + ox, v * 3f + oy);
        float n2 = Perlin(u * 6f + ox, v * 6f + oy) * 0.5f;
        float n3 = Perlin(u * 12f + ox, v * 12f + oy) * 0.25f;
        float continentMask = Mathf.Clamp01(n1 + n2 + n3 - 0.55f) * 3f;

        // Calote polare
        float polarN = Mathf.Clamp01((v - 0.88f) * 10f);
        float polarS = Mathf.Clamp01((0.12f - v) * 10f);
        float polar  = Mathf.Max(polarN, polarS);

        // Nori subtiri
        float cloud = Perlin(u * 7f + ox + 50f, v * 7f + oy + 50f);
        cloud = Mathf.Clamp01((cloud - 0.6f) * 5f) * 0.4f;

        Color surface = continentMask < 0.5f
            ? Color.Lerp(ocean, land,   continentMask * 2f)
            : Color.Lerp(land,  desert, (continentMask - 0.5f) * 2f);

        surface = Color.Lerp(surface, Color.white, polar);
        return Color.Lerp(surface, new Color(0.95f, 0.95f, 0.98f), cloud);
    }

    // Soare: suprafata incandescenta, granulatie solara
    private static Color SampleSun(float u, float v, float ox, float oy,
                                   Color hotYellow, Color coolOrange, Color midOrange)
    {
        float n1 = Perlin(u * 5f + ox, v * 5f + oy);
        float n2 = Perlin(u * 10f + ox, v * 10f + oy) * 0.5f;
        float n3 = Perlin(u * 20f + ox, v * 20f + oy) * 0.25f;
        float t  = Mathf.Clamp01(n1 + n2 + n3);

        // Pete solare (spot-uri intunecate)
        float spots = 0f;
        for (int i = 0; i < 4; i++)
        {
            float cx = Mathf.Repeat(ox * 0.005f + i * 0.28f, 1f);
            float cy = 0.3f + (i % 2) * 0.2f;
            float dx = (u - cx) * 5f;
            float dy = (v - cy) * 5f;
            float r  = dx * dx + dy * dy;
            if (r < 0.06f) spots += (0.06f - r) * 10f;
        }
        spots = Mathf.Clamp01(spots * 0.5f);

        Color base_ = t < 0.5f ? Color.Lerp(midOrange, hotYellow, t * 2f)
                                : Color.Lerp(hotYellow, new Color(1f, 1f, 0.7f), (t - 0.5f) * 2f);

        return Color.Lerp(base_, coolOrange * 0.5f, spots);
    }

    // ─── Helper Perlin (tiling pe U ca sa evitam cusatura la 180 grade) ─────────

    private static float Perlin(float x, float y)
    {
        // Folosim Mathf.PerlinNoise direct, simplu si suficient
        return Mathf.PerlinNoise(x, y);
    }
}
