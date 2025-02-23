# Changelog

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