# RBDynamicBones
This is a Unity+MarrowSDK tool that'll take a hierarchy of Bones and set it up as a Dynamic-Bone Esque physics chain.
As this tool is meant for bonelab, it depends on the [Extended SDK](https://github.com/notnotnotswipez/Marrow-ExtendedSDK-MAINTAINED) & [Noodled Events](https://github.com/holadivinus/Noodled-Events).

Main Features: \
 \- Works in Mirrors \
  \- Works in Editor "Play Mode" \
   \- DeOffsets Head/Hair Bones for Self-Interaction \
   \- Can be Animated with the "AnimRig" (Requires good Elasticity) \
   \- Supports Fusion 

Please do note that this is a Beta; 
Let me know if you run into any issues In [this](https://discord.com/channels/563139253542846474/1344491437676560467) thread within the [Bonelab Community Discord](https://discord.gg/Js2kqYJq).

## Installation:
1. Setup a Project with the [Extended SDK](https://github.com/notnotnotswipez/Marrow-ExtendedSDK-MAINTAINED)
2. Install [Noodled Events](https://github.com/holadivinus/Noodled-Events)
3. In the created project, click "Window" -> "Package Manager" -> Top Left Plus Button -> "Add Package from Git URL"
4. Paste this link: `https://github.com/holadivinus/RBDynBones.git`
5. Press Add

## Usage:
1. Create RBDynamicBones / "PhysBones" by adding the "PhysBoneMaker" component to a bone.
2. Configure your bone's properties (check the "Spec" section below)
3. "Compile" this bone by Right Clicking the Component Title -> Pressing "Finalize for Bonelab" (see below image)


![Image displaying the Component's Context Menu](https://cdn.discordapp.com/attachments/1344491437676560467/1477888860250701976/image.png?ex=69a86102&is=69a70f82&hm=4433f30ab6999a9e957870fdb950945ea574257045b49a643d76aa1c74c779d2&)
(Additionally, you can remove a bone entirely by pressing "Remove Generated Objects",
Followed by "Remove Component".)

Bonus Tip - You can "Recompile" all bones in a Hierarchy (or Avatar) by Right Clicking the Root Transform,
Followed by pressing "Finalize All PhysBones" (see below image)
![Image displaying Root Transform Context Menu](https://cdn.discordapp.com/attachments/1344491437676560467/1477888860766736505/image.png?ex=69a86102&is=69a70f82&hm=a92caab458985eeaf89c4cb8df322ef0903b2532162b2843f63f0fb5ea64864e&)

## Spec
<ins>PhysBoneMaker:</ins> \
\- Max Sub Joints (max # of trailing sub joints) \
 \- Collider Scale (scale of bone colliders) \
 \- Collider Curve (curve to determine sub joint collider size by length) \
 \- Elasticity (how hard the bone tries to go to its AnimRig pos) \
 \- Visual Smoothing (how fast the visual matches up to the PhysicsRig) \
  \- Mirror Rotation Sync Mode Dropdown: \
  \\- MirrorSyncMode.Tweened (Mirror Bones will sync via animations - can cause short but extreme rotation desync) \
  \\- MirrorSyncMode.Direct (Mirror Bones will sync via rotation copy - slower, but perfectly accurate.) \
 \- Drag (sub-joint drag, doesn't effect air resistance) \
 \- Mass (sub-joint mass) \
 \- [Optional] Rig Inheritance (what part of the physics rig is this connected to?) \
 \- [Optional] Rig Inheritance Factor (how much of the physics rig velocity should transfer to this bone?) 

As this tool is a tad bit unpolished, so please do add bones to a Prefab Variant of your avatar.
If anything breaks you can revert the variant, and then let me know in the [Linked Thread](https://discord.com/channels/563139253542846474/1344491437676560467).
Thanks, have fun!


