# PhysBone Converter
This tool will convert your PhysBones and PBColliders to DynamicBones and DBColliders.<br>
Of course, it's not perfect but it does almost the inverse conversion of PhysBones.<br><br>
<a href=https://github.com/Dreadrith/PhysBone-Converter/releases/latest/download/PhysBoneConverter.unitypackage>Download here</a><br><br>
![window](https://raw.githubusercontent.com/Dreadrith/DreadScripts/main/Other/Info_Source/PhysBoneConverter/toolwindow.png)

# How to use
Prerequisites: <a href=https://vrchat.com/home/download>VRCAvatars SDK</a> & <a href=https://assetstore.unity.com/packages/tools/animation/dynamic-bone-16743>Dynamic Bone</a> OR <a href=https://github.com/Markcreator/VRChat-Tools>Dynamic Bone Container</a>

Find the tool's window near the top left corner of Unity under<br>
DreadTools > Utility > PhysBone Converter

1- Make sure that your avatar or root is correctly set as "Root".<br>
2- Press Convert. Done!

You can optionally disable the "Auto" option for any setting and set your own value and curve.<br>
This will be the same value across all DynamicBones<br><br>
![showcase](https://raw.githubusercontent.com/Dreadrith/DreadScripts/main/Other/Info_Source/PhysBoneConverter/showcase.gif)

# Potential Issues
Issue: DreadTools button doesn't appear on the top toolbar<br>
Solution: Your project has a script error which prevents new scripts from loading. Check your console for errors and read them for details about what's causing them. Below are some common errors

Issue: Error: the type or namespace name 'DynamicBoneColliderBase' could not be found.<br>
Solution: You either don't have Dynamic Bones or your dynamic bones version doesn't match the one this script is made for. This was made for 1.2.x as it's the most common version. Try using the Dynamic Bone Container listed above instead

Issue: Error: The type or namespace name 'VRCPhysBoneCollider' could not be found<br>
Issue: Error: 'VRCPhysBone' does not contain a definition for 'maxAngleXCurve'...<br>
Solution: Both of these can be solved with importing the proper Avatars VRCSDK. Currently, the latest Avatars VRCSDK should work.

Issue: The Dynamic Bones aren't acting the same as how PhysBones were.<br>
Sadly, Dynamic Bones don't function the same as PhysBones and have less features. As such, they will behave differently and in some uncommon cases even look broken in comparison. This may be improved with manual tweaking.
