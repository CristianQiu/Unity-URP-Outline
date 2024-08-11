# About

* Unity package that includes a simple outline render feature for Unity 6 URP and rendergraph.
* Integrated with URP volume system.
* Uses rendering layers.
* Perfect for fast prototyping, since no other components-shaders are needed.

# How to add an outlined object

* Add a rendering layer with the name "Outline". You can do it in "Edit->Project Settings->Tags and Layers".
* Add the outline renderer feature to you URP Renderer.
* Enable post-processing both in your camera and URP Renderer.
* Add a volume to your scene and add override: "Custom->Outline".
* Make sure the "Color" parameter has alpha greater than 0, otherwise it will be considered as disabled.
* Add the "Outline" rendering layer to the rendering layer mask of the object you want to be outlined, either through the UI or using code.

# Known limitations

* Does not support Unity's rendergraph compatibility mode.
* Different outline colors per each outlined object is not currently supported.
* If you want to apply some sort of antialiasing to the outline, using Unity's FXAA is currently the only option.
* Since postprocessing is not applied to the outline, some effects like HDR glow won't work.
* Changing the resolution of the game will result in different outline widths.

# Preview
![alt-text](https://github.com/CristianQiu/Unity-Packages-Gifs/blob/main/URP-Outline/Teaser.jpg)
