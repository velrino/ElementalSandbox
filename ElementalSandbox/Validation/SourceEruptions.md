# Desktop VFX revision — source-based eruption layers

Implemented in C# / URP HLSL:

- VenomSurgeAbility / MonolithRiftAbility: three populations, spatial distribution, height and radius profiles, tilt, stagger, emergence overshoot, settling and sinking. Fixed dice per cast make visual comparisons reproducible.
- Original crystal and monolith generators exported with each effect's settings. Monolith fresh-fracture attributes are preserved.
- VenomCrystalMaterial: local-space fluid, cleavage, depth tint, milky tips, RGB rim dispersion, facet glints, core lighting and per-instance birth flash. Uses URP PBR lighting.
- MonolithStoneMaterial: the original four rock textures, world-space triplanar projection, normal mapping, roughness, AO, fresh faces, damp roots, grading and settling dust.
- Original ShatterGeometry Voronoi meshes: centroid gaps, radial growth, heave, tilt and withdrawal.
- Separate aerosol and ballistic particle systems use source palettes, lifetimes, gravity and breach emitters. Quake has an expanding dust ring and bouncing solid debris.
- Corrected the Three.js-to-Unity coordinate conversion for sun and rim directions; added real point lights to the two eruptions.

Remaining fidelity differences: aerosol rendering uses Unity particles with an adapted simplex-noise shader rather than the complete JS particle engine; the core is still the earlier billboard; fissure decals, the full source plate shading, HDR environment reflections and some secondary emitters are not yet ported. Procedural mesh shape controls are exported at their default values, while field dimensions and surface settings update at runtime. Other abilities retain their earlier implementations. This is not a claim of complete visual parity.

Validation: see NativeDesktop screenshots/result.txt and SmokeTest.txt. Native checks execute all ten abilities; smoke checks also cover pooling, pause and body slicing. Screenshots establish rendering, not measured performance or full visual equivalence.
