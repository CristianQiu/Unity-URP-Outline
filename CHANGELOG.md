# Changelog

## [1.0.0] - 2026-03-11

* Unity 6000.3.0 or above is now required.
* Removed the possibility of adding softness to the outline, which allows considerable optimizations and should make a measurable difference on lower end devices such as smartphones or Steam Deck.
* The outline border may have lesser quality as the border size increases, so really wide borders will not look perfectly clean or filled, depending on the geometry being rendered.
* The outline performance is resolution independant now and scales better to keep its size at different resolutions and/or render scales.
* If you want to keep using the outline softness you should stay on 0.3.3 version, the version has been tagged so you can download the zipped files from 0.3.3 easily.

## [0.3.3] - 2025-05-24

* Outline is scaled to preserve its width with a 1440p reference resolution.

## [0.3.2] - 2025-05-22

* Move the outline to before rendering postprocessing so it works with postprocessing and HDR colors. Fixes issue with camera stacking not rendering the outline, and TAA seems to work slightly better now, specially with STP enabled.

## [0.3.1] - 2025-02-23

* Removed empty version dependency as it was throwing errors. User should make sure that they have URP package installed before installing the outline package to avoid console errors.

## [0.3] - 2024-02-18

* Slightly improved performance by rendering the [four outline masks into a single texture](https://github.com/CristianQiu/Unity-URP-Outline/pull/1).

## [0.2] - 2024-11-13

* Added up to 4 layers for different colors, each one associated to a rendering layer mask.

## [0.1.21] - 2024-09-24

* Prevent warning when using GPU resident drawer.

## [0.1.2] - 2024-09-23

* Added support for the GPU resident drawer.

## [0.1.1] - 2024-09-23

* Added fill alpha parameter to also color the inside of the object.

## [0.1.0] - 2024-08-11

* Initial Release. Verified in Unity 6000.0.14f1.