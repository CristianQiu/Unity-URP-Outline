# About

* Unity package that includes a simple outline render feature for Unity 6 URP and rendergraph.
* Integrated with URP volume system.
* Uses rendering layers.
* Perfect for fast prototyping, since no other components-shaders are needed.

# How to add an outlined object

* Add 4 rendering layers with the name "Outline_1", "Outline_2", "Outline_3" and "Outline_4". You can do it in "Edit->Project Settings->Tags and Layers".
* Add the outline renderer feature to you URP Renderer.
* Enable post-processing both in your camera and URP Renderer.
* Add a volume to your scene and add override: "Custom->Outline".
* Add the "Outline_1" rendering layer to the rendering layer mask of the object you want to be outlined, either through the UI or using code. The same applies to the rest of the layers that you should have added before. Each layer has different parameters in the outline volume, with the exception of outline width, represented by the "Blur Radius" parameter, which is the same for all of them for technical & performance reasons.

# Known limitations

* Does not support Unity's rendergraph compatibility mode.
* Different outline widths per each outlined object is not currently supported.
* If you want to apply some sort of antialiasing to the outline, using Unity's FXAA is currently the only option.
* Since postprocessing is not applied to the outline, some effects like HDR glow won't work.
* Changing the resolution of the game will result in slightly different outline widths.
* Some postprocessing effects like exaggerated Panini Projection may break the connection between the object and its outline, since the outline is applied after postprocessing by default. 

# Preview
![alt-text](https://github.com/CristianQiu/Unity-Packages-Gifs/blob/main/URP-Outline/Teaser.jpg)
