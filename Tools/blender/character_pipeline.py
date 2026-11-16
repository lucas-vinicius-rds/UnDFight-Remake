r"""
UnDFight Remake - Reusable Character Pipeline (Blender 5.2+ bpy)
Supports cleaning, selective decimation, Unity Humanoid rig creation,
weight refinement, 60 FPS fighting-game animation generation, and FBX export.

Usage inside Blender:
    import sys
    sys.path.append(r"D:\Programas SSD\Unity\Unity Projects\UnDFight Remake\Tools\blender")
    import character_pipeline
    character_pipeline.run_animations_and_export("Lucas")
"""

import os
import math
import bpy
import bmesh
from mathutils import Vector, Euler

PROJECT_ROOT = r"D:\Programas SSD\Unity\Unity Projects\UnDFight Remake"

WEIGHT_CLASS_FRAMEDATA = {
    "light": {
        "LightPunch": (3, 3, 7),    # 13 frames
        "HeavyPunch": (5, 4, 12),   # 21 frames
        "LightKick":  (4, 3, 9),    # 16 frames
        "HeavyKick":  (7, 4, 16),   # 27 frames
        "CrouchKick": (6, 4, 15),   # 25 frames
        "JumpAttack": (4, 8, 6),    # 18 frames
        "Throw":      (5, 3, 14),   # 22 frames
        "Special":    (7, 4, 13),   # 24 frames (Jompes default light)
    },
    "medium": {
        "LightPunch": (4, 3, 9),    # 16 frames
        "HeavyPunch": (7, 4, 16),   # 27 frames
        "LightKick":  (5, 3, 11),   # 19 frames
        "HeavyKick":  (9, 4, 20),   # 33 frames
        "CrouchKick": (7, 4, 17),   # 28 frames
        "JumpAttack": (4, 8, 6),    # 18 frames
        "Throw":      (5, 3, 14),   # 22 frames
        "Special":    (8, 5, 15),   # 28 frames (Tiago)
    },
    "heavy": {
        "LightPunch": (5, 3, 11),   # 19 frames
        "HeavyPunch": (9, 5, 19),   # 33 frames
        "LightKick":  (6, 4, 13),   # 23 frames
        "HeavyKick":  (11, 5, 24),  # 40 frames
        "CrouchKick": (8, 5, 19),   # 32 frames
        "JumpAttack": (4, 8, 6),    # 18 frames
        "Throw":      (5, 3, 14),   # 22 frames
        "Special":    (10, 5, 17),  # 32 frames (Lucas)
    },
}

STYLE_FRAMEDATA_OVERRIDES = {
    "agile_striker": {
        "Special": (6, 4, 12),      # 22 frames (Rochard: Tornado Relâmpago)
    },
}


def get_weight_class(weight_kg):
    if weight_kg <= 75.0:
        return "light"
    if weight_kg <= 90.0:
        return "medium"
    return "heavy"


def get_fighter_framedata(config=None):
    config = config or {}
    style = config.get("fighting_style", "heavy_brawler")
    weight_class = config.get("weight_class") or get_weight_class(config.get("weight_kg", 92.0))
    fd = dict(WEIGHT_CLASS_FRAMEDATA[weight_class])
    if style in STYLE_FRAMEDATA_OVERRIDES:
        fd.update(STYLE_FRAMEDATA_OVERRIDES[style])
    if config.get("framedata_overrides"):
        fd.update(config["framedata_overrides"])
    return weight_class, style, fd


CHARACTER_CONFIGS = {
    "Lucas": {
        "name": "Lucas",
        "archetype_label": "Heavy Brawler",
        "weight_kg": 92.0,
        "weight_class": "heavy",
        "fighting_style": "heavy_brawler",
        "mesh_name": "Lucas_Body",
        "armature_name": "Lucas_Armature",
        "material_name": "Lucas_Mat",
        "source_fbx": os.path.join(
            PROJECT_ROOT,
            r"Assets\UnDFight\Art\Source\Lucas\Meshy_AI_Man_in_Black_biped_Animation_Running_withSkin_RigCorrigido.fbx",
        ),
        "textures_dir": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Lucas"),
        "orig_blend": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Blend\Lucas_original.blend"),
        "work_blend": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Blend\Lucas.blend"),
        "export_fbx": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Models\Lucas\Lucas.fbx"),
        "target_height": 1.81,
        "target_tris": 30000,
        "body_tris_target": 11200,
        "hair_z_threshold": 1.48,
        "center_x": 0.0035,
        "fps": 60,
        "anim_overrides": {},
        "joints": {
            "Hips":          ((0.0035, -0.005, 0.922), (0.0035, -0.005, 1.065)),
            "Spine":         ((0.0035, -0.005, 1.065), (0.0035, -0.010, 1.209)),
            "Chest":         ((0.0035, -0.010, 1.209), (0.0035, -0.003, 1.352)),
            "UpperChest":    ((0.0035, -0.003, 1.352), (0.0035,  0.010, 1.465)),
            "Neck":          ((0.0035,  0.010, 1.465), (0.0035,  0.018, 1.537)),
            "Head":          ((0.0035,  0.018, 1.537), (0.0035,  0.018, 1.780)),
            "LeftShoulder":  ((0.0395,  0.010, 1.416), (0.1835,  0.023, 1.416)),
            "LeftUpperArm":  ((0.1835,  0.023, 1.416), (0.4139,  0.033, 1.368)),
            "LeftLowerArm":  ((0.4139,  0.033, 1.368), (0.6443,  0.012, 1.366)),
            "LeftHand":      ((0.6443,  0.012, 1.366), (0.7350,  0.000, 1.360)),
            "RightShoulder": ((-0.0355, 0.010, 1.416), (-0.1913, 0.022, 1.416)),
            "RightUpperArm": ((-0.1913, 0.022, 1.416), (-0.3983, 0.032, 1.366)),
            "RightLowerArm": ((-0.3983, 0.032, 1.366), (-0.6326, 0.015, 1.368)),
            "RightHand":     ((-0.6326, 0.015, 1.368), (-0.7250, 0.000, 1.360)),
            "LeftUpperLeg":  ((0.1031, -0.005, 0.845), (0.1445, -0.028, 0.538)),
            "LeftLowerLeg":  ((0.1445, -0.028, 0.538), (0.1952,  0.029, 0.147)),
            "LeftFoot":      ((0.1952,  0.029, 0.147), (0.2236, -0.087, 0.040)),
            "LeftToes":      ((0.2236, -0.087, 0.040), (0.2450, -0.210, 0.040)),
            "RightUpperLeg": ((-0.0955, -0.005, 0.845), (-0.1367, -0.029, 0.542)),
            "RightLowerLeg": ((-0.1367, -0.029, 0.542), (-0.1835,  0.031, 0.155)),
            "RightFoot":     ((-0.1835,  0.031, 0.155), (-0.2177, -0.086, 0.040)),
            "RightToes":     ((-0.2177, -0.086, 0.040), (-0.2400, -0.210, 0.040)),
        },
    },
    # Template stub for Phase 4 (Tiago, 1.79 m)
    "Tiago": {
        "name": "Tiago",
        "archetype_label": "Street Scrappy",
        "weight_kg": 87.0,
        "weight_class": "medium",
        "fighting_style": "street_scrappy",
        "mesh_name": "Tiago_Body",
        "armature_name": "Tiago_Armature",
        "material_name": "Tiago_Mat",
        "source_fbx": os.path.join(
            PROJECT_ROOT,
            r"Assets\UnDFight\Art\Source\Tiago\Meshy_AI_Casual_Smiling_Man_wi_biped_Animation_Running_withSkin.fbx",
        ),
        "textures_dir": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Tiago"),
        "orig_blend": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Blend\Tiago_original.blend"),
        "work_blend": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Blend\Tiago.blend"),
        "export_fbx": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Models\Tiago\Tiago.fbx"),
        "target_height": 1.79,
        "target_tris": 30000,
        "body_tris_target": 12000,
        "hair_z_threshold": 1.48,
        "center_x": 0.0025,
        "fps": 60,
        "anim_overrides": {},
        "joints": {
            "Hips":          ((0.0038, -0.0110, 0.9668), (0.0032, -0.0022, 1.0948)),
            "Spine":         ((0.0032, -0.0022, 1.0948), (0.0026,  0.0065, 1.2227)),
            "Chest":         ((0.0026,  0.0065, 1.2227), (0.0019,  0.0153, 1.3507)),
            "UpperChest":    ((0.0019,  0.0153, 1.3507), (0.0008,  0.0217, 1.4436)),
            "Neck":          ((0.0008,  0.0217, 1.4436), (0.0000,  0.0266, 1.5152)),
            "Head":          ((0.0000,  0.0266, 1.5152), (0.0000,  0.0100, 1.7750)),
            "LeftShoulder":  ((0.0402,  0.0287, 1.4009), (0.1931,  0.0420, 1.4009)),
            "LeftUpperArm":  ((0.1931,  0.0420, 1.4009), (0.4093,  0.0779, 1.3159)),
            "LeftLowerArm":  ((0.4093,  0.0779, 1.3159), (0.6488,  0.0731, 1.2773)),
            "LeftHand":      ((0.6488,  0.0731, 1.2773), (0.7450,  0.0680, 1.2680)),
            "RightShoulder": ((-0.0371, 0.0285, 1.3970), (-0.1931, 0.0416, 1.3970)),
            "RightUpperArm": ((-0.1931, 0.0416, 1.3970), (-0.3900, 0.0766, 1.3159)),
            "RightLowerArm": ((-0.3900, 0.0766, 1.3159), (-0.6294, 0.0796, 1.2850)),
            "RightHand":     ((-0.6294, 0.0796, 1.2850), (-0.7250, 0.0730, 1.2750)),
            "LeftUpperLeg":  ((0.0882, -0.0093, 0.8737), (0.1236, -0.0025, 0.5011)),
            "LeftLowerLeg":  ((0.1236, -0.0025, 0.5011), (0.1429,  0.0698, 0.1458)),
            "LeftFoot":      ((0.1429,  0.0698, 0.1458), (0.1660, -0.0516, 0.0457)),
            "LeftToes":      ((0.1660, -0.0516, 0.0457), (0.1957, -0.2079, 0.0457)),
            "RightUpperLeg": ((-0.0778, -0.0098, 0.8729), (-0.1120, -0.0049, 0.4973)),
            "RightLowerLeg": ((-0.1120, -0.0049, 0.4973), (-0.1313,  0.0654, 0.1458)),
            "RightFoot":     ((-0.1313,  0.0654, 0.1458), (-0.1661, -0.0522, 0.0455)),
            "RightToes":     ((-0.1661, -0.0522, 0.0455), (-0.2111, -0.2040, 0.0455)),
        },
    },
    "Jompes": {
        "name": "Jompes",
        "archetype_label": "Technical Striker",
        "weight_kg": 70.0,
        "weight_class": "light",
        "fighting_style": "technical_striker",
        "mesh_name": "Jompes_Body",
        "armature_name": "Jompes_Armature",
        "material_name": "Jompes_Mat",
        "source_fbx": os.path.join(
            PROJECT_ROOT,
            r"Assets\UnDFight\Art\Source\Jompes\Meshy_AI_Shadow_Sentinel_biped_Animation_Running_withSkin.fbx",
        ),
        "textures_dir": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Jompes"),
        "orig_blend": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Blend\Jompes_original.blend"),
        "work_blend": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Blend\Jompes.blend"),
        "export_fbx": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Models\Jompes\Jompes.fbx"),
        "target_height": 1.65,
        "target_tris": 30000,
        "body_tris_target": 12000,
        "hair_z_threshold": 1.36,
        "center_x": 0.0035,
        "fps": 60,
        "anim_overrides": {},
        "joints": {
            "Hips":          ((0.0035, -0.0106, 0.9375), (0.0041, -0.0026, 1.0476)),
            "Spine":         ((0.0041, -0.0026, 1.0476), (0.0046,  0.0054, 1.1577)),
            "Chest":         ((0.0046,  0.0054, 1.1577), (0.0051,  0.0133, 1.2678)),
            "UpperChest":    ((0.0051,  0.0133, 1.2678), (0.0060,  0.0188, 1.3434)),
            "Neck":          ((0.0060,  0.0188, 1.3434), (0.0067,  0.0236, 1.4094)),
            "Head":          ((0.0067,  0.0236, 1.4094), (0.0067,  0.0100, 1.6350)),
            "LeftShoulder":  ((0.0383,  0.0231, 1.3020), (0.1709,  0.0328, 1.3020)),
            "LeftUpperArm":  ((0.1709,  0.0328, 1.3020), (0.3880,  0.0544, 1.2593)),
            "LeftLowerArm":  ((0.3880,  0.0544, 1.2593), (0.5980,  0.0492, 1.2557)),
            "LeftHand":      ((0.5980,  0.0492, 1.2557), (0.6880,  0.0400, 1.2500)),
            "RightShoulder": ((-0.0308, 0.0226, 1.2984), (-0.1744, 0.0320, 1.2984)),
            "RightUpperArm": ((-0.1744, 0.0320, 1.2984), (-0.3702, 0.0515, 1.2557)),
            "RightLowerArm": ((-0.3702, 0.0515, 1.2557), (-0.5802, 0.0529, 1.2593)),
            "RightHand":     ((-0.5802, 0.0529, 1.2593), (-0.6700, 0.0420, 1.2520)),
            "LeftUpperLeg":  ((0.0845, -0.0074, 0.8495), (0.1175,  0.0052, 0.4975)),
            "LeftLowerLeg":  ((0.1175,  0.0052, 0.4975), (0.1424,  0.0628, 0.1202)),
            "LeftFoot":      ((0.1424,  0.0628, 0.1202), (0.1569, -0.0436, 0.0370)),
            "LeftToes":      ((0.1569, -0.0436, 0.0370), (0.1753, -0.1783, 0.0370)),
            "RightUpperLeg": ((-0.0772,-0.0078, 0.8488), (-0.1103, 0.0034, 0.4939)),
            "RightLowerLeg": ((-0.1103, 0.0034, 0.4939), (-0.1317, 0.0649, 0.1202)),
            "RightFoot":     ((-0.1317, 0.0649, 0.1202), (-0.1529,-0.0422, 0.0386)),
            "RightToes":     ((-0.1529,-0.0422, 0.0386), (-0.1793,-0.1758, 0.0386)),
        },
    },
    "Rochard": {
        "name": "Rochard",
        "archetype_label": "Agile Striker",
        "weight_kg": 67.0,
        "weight_class": "light",
        "fighting_style": "agile_striker",
        "mesh_name": "Rochard_Body",
        "armature_name": "Rochard_Armature",
        "material_name": "Rochard_Mat",
        "source_fbx": os.path.join(
            PROJECT_ROOT,
            r"Assets\UnDFight\Art\Source\Rochard\Meshy_AI_Vasco_Jacket_Avatar_biped_Animation_Running_withSkin.fbx",
        ),
        "textures_dir": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Rochard"),
        "orig_blend": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Blend\Rochard_original.blend"),
        "work_blend": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Source\Blend\Rochard.blend"),
        "export_fbx": os.path.join(PROJECT_ROOT, r"Assets\UnDFight\Art\Models\Rochard\Rochard.fbx"),
        "target_height": 1.75,
        "target_tris": 30000,
        "body_tris_target": 12000,
        "hair_z_threshold": 1.44,
        "center_x": 0.0038,
        "fps": 60,
        "anim_overrides": {},
        "joints": {
            "Hips":          ((0.0038, -0.0004, 0.9490), (0.0038,  0.0062, 1.0767)),
            "Spine":         ((0.0038,  0.0062, 1.0767), (0.0038,  0.0127, 1.2044)),
            "Chest":         ((0.0038,  0.0127, 1.2044), (0.0038,  0.0193, 1.3322)),
            "UpperChest":    ((0.0038,  0.0193, 1.3322), (0.0038,  0.0241, 1.4264)),
            "Neck":          ((0.0038,  0.0241, 1.4264), (0.0038,  0.0277, 1.4964)),
            "Head":          ((0.0038,  0.0277, 1.4964), (0.0038,  0.0100, 1.7350)),
            "LeftShoulder":  ((0.0393,  0.0314, 1.3884), (0.1812,  0.0435, 1.3884)),
            "LeftUpperArm":  ((0.1812,  0.0435, 1.3884), (0.4078,  0.0803, 1.3243)),
            "LeftLowerArm":  ((0.4078,  0.0803, 1.3243), (0.6645,  0.0602, 1.3092)),
            "LeftHand":      ((0.6645,  0.0602, 1.3092), (0.7600,  0.0500, 1.3130)),
            "RightShoulder": ((-0.0340, 0.0313, 1.3847), (-0.1850, 0.0434, 1.3847)),
            "RightUpperArm": ((-0.1850, 0.0434, 1.3847), (-0.3964, 0.0798, 1.3205)),
            "RightLowerArm": ((-0.3964, 0.0798, 1.3205), (-0.6494, 0.0650, 1.3130)),
            "RightHand":     ((-0.6494, 0.0650, 1.3130), (-0.7450, 0.0520, 1.3140)),
            "LeftUpperLeg":  ((0.0836,  0.0001, 0.8587), (0.1170,  0.0022, 0.4974)),
            "LeftLowerLeg":  ((0.1170,  0.0022, 0.4974), (0.1284,  0.0759, 0.1425)),
            "LeftFoot":      ((0.1284,  0.0759, 0.1425), (0.1526, -0.0481, 0.0412)),
            "LeftToes":      ((0.1526, -0.0481, 0.0412), (0.1836, -0.2071, 0.0412)),
            "RightUpperLeg": ((-0.0789, 0.0003, 0.8572), (-0.1133, 0.0031, 0.4899)),
            "RightLowerLeg": ((-0.1133, 0.0031, 0.4899), (-0.1208, 0.0719, 0.1388)),
            "RightFoot":     ((-0.1208, 0.0719, 0.1388), (-0.1489,-0.0497, 0.0434)),
            "RightToes":     ((-0.1489,-0.0497, 0.0434), (-0.1844,-0.2028, 0.0434)),
        },
    },
}


# ---------------------------------------------------------------------------
# Pose & Animation Helpers
# ---------------------------------------------------------------------------

def set_scene_fps(fps=60):
    scene = bpy.context.scene
    scene.render.fps = fps
    scene.render.fps_base = 1.0


def reset_pose(arm_obj):
    for pb in arm_obj.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)


def set_bone_world_rot(arm_obj, bone_name, rx=0.0, ry=0.0, rz=0.0, order='XYZ'):
    """Apply world-space Euler rotation (degrees) relative to bone rest pose."""
    pb = arm_obj.pose.bones[bone_name]
    r_world = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), order).to_matrix()
    m_rest = pb.bone.matrix_local.to_3x3()
    r_local = m_rest.inverted() @ r_world @ m_rest
    pb.rotation_mode = 'QUATERNION'
    pb.rotation_quaternion = r_local.to_quaternion()


def set_hips_world_offset(arm_obj, dx=0.0, dy=0.0, dz=0.0):
    """Translate Hips in world space (meters). Use dx=0, dy=0 for zero horizontal root motion."""
    pb = arm_obj.pose.bones["Hips"]
    m_rest = pb.bone.matrix_local.to_3x3()
    pb.location = m_rest.inverted() @ Vector((dx, dy, dz))


def apply_pose_dict(arm_obj, pose_dict):
    reset_pose(arm_obj)
    if "HipsOffset" in pose_dict:
        ox, oy, oz = pose_dict["HipsOffset"]
        set_hips_world_offset(arm_obj, ox, oy, oz)
    for bone_name, rot in pose_dict.items():
        if bone_name == "HipsOffset":
            continue
        order = rot[3] if len(rot) > 3 else 'XYZ'
        set_bone_world_rot(arm_obj, bone_name, rot[0], rot[1], rot[2], order=order)


def key_all_deform_bones(arm_obj, frame):
    for pb in arm_obj.pose.bones:
        if not pb.bone.use_deform:
            continue
        pb.rotation_mode = 'QUATERNION'
        pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
        if pb.name == "Hips":
            pb.keyframe_insert(data_path="location", frame=frame)


def _finger_curl_pose(finger_angles, thumb_angles, intensity=1.0):
    """Build 30 finger bone rotations for both hands using ZYX order (adduct Z first, then curl Y)."""
    pose = {}
    scale = max(0.0, float(intensity))
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        for fname, (p_curl, i_curl, d_curl, adduct_z) in finger_angles.items():
            # Positive ry on Left (+X arm) and negative ry on Right (-X arm) curls palmward (-Z)
            pose[f"{side}{fname}Proximal"] = (0.0, sign * p_curl * scale, sign * adduct_z * scale, 'ZYX')
            pose[f"{side}{fname}Intermediate"] = (0.0, sign * i_curl * scale, 0.0, 'ZYX')
            pose[f"{side}{fname}Distal"] = (0.0, sign * d_curl * scale, 0.0, 'ZYX')
        for seg, (tx, ty, tz) in zip(("Proximal", "Intermediate", "Distal"), thumb_angles):
            pose[f"{side}Thumb{seg}"] = (tx * scale, sign * ty * scale, sign * tz * scale, 'ZYX')
    return pose


def closed_fist(intensity=1.0):
    """Anatomical closed combat fist pose for all 30 finger bones."""
    # Adduct fingers side-by-side around Z first, then curl tightly into the palm
    fingers = {
        "Index":  (95.0, 112.0, 80.0,  9.5),
        "Middle": (96.0, 114.0, 82.0,  2.5),
        "Ring":   (96.0, 114.0, 82.0, -4.5),
        "Little": (95.0, 112.0, 80.0, -9.5),
    }
    # Thumb reaches forward/inward at CMC then wraps its IP joints across Index/Middle phalanges
    thumb = [
        (12.0, 28.0, 38.0),
        (6.0,  30.0, 50.0),
        (2.0,  24.0, 38.0),
    ]
    return _finger_curl_pose(fingers, thumb, intensity=intensity)


def relaxed_grip(intensity=0.45):
    """Semi-open natural hand pose for HitReact, KnockDown, GetUp and Defeat."""
    fingers = {
        "Index":  (34.0, 44.0, 30.0,  3.0),
        "Middle": (38.0, 48.0, 34.0,  0.5),
        "Ring":   (42.0, 52.0, 38.0, -2.0),
        "Little": (46.0, 56.0, 42.0, -4.0),
    }
    thumb = [
        (8.0,  10.0, 14.0),
        (4.0,  12.0, 22.0),
        (2.0,  10.0, 16.0),
    ]
    return _finger_curl_pose(fingers, thumb, intensity=intensity / 0.45)


def _collect_foot_vertex_groups(mesh_obj):
    """Identify Left/Right foot heel & toe vertex indices from rest mesh + vertex groups."""
    if mesh_obj is None:
        return None
    vgs = mesh_obj.vertex_groups
    vg_ids = {}
    for bname in ("LeftFoot", "LeftToes", "RightFoot", "RightToes"):
        vg = vgs.get(bname)
        vg_ids[bname] = vg.index if vg is not None else -1

    data = {
        "Left": {"all": [], "heel": [], "toe": []},
        "Right": {"all": [], "heel": [], "toe": []},
    }
    for v in mesh_obj.data.vertices:
        weights = {g.group: g.weight for g in v.groups}
        for side in ("Left", "Right"):
            wf = weights.get(vg_ids[f"{side}Foot"], 0.0)
            wt = weights.get(vg_ids[f"{side}Toes"], 0.0)
            if wf + wt >= 0.35:
                data[side]["all"].append(v.index)
                if wt >= 0.30:
                    data[side]["toe"].append(v.index)
                elif wf >= 0.35:
                    data[side]["heel"].append(v.index)

    # Fallback if Toes group is tiny: split by rest-pose Y near mid-foot
    for side in ("Left", "Right"):
        all_idx = data[side]["all"]
        if not all_idx:
            continue
        if len(data[side]["heel"]) < 4 or len(data[side]["toe"]) < 4:
            ys = [mesh_obj.data.vertices[i].co.y for i in all_idx]
            mid_y = 0.5 * (min(ys) + max(ys))
            data[side]["toe"] = [i for i in all_idx if mesh_obj.data.vertices[i].co.y < mid_y]
            data[side]["heel"] = [i for i in all_idx if mesh_obj.data.vertices[i].co.y >= mid_y]
    return data


def _eval_mesh_min_z(mesh_obj, vertex_indices=None):
    """Evaluate deformed world-space min Z over specified vertices (or whole mesh)."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_obj = mesh_obj.evaluated_get(depsgraph)
    eval_mesh = eval_obj.to_mesh()
    mw = eval_obj.matrix_world
    try:
        verts = eval_mesh.vertices
        if vertex_indices:
            min_z = min((mw @ verts[i].co).z for i in vertex_indices)
        else:
            min_z = min((mw @ v.co).z for v in verts)
    finally:
        eval_obj.to_mesh_clear()
    return min_z


def _get_bone_world_rx_deg(arm_obj, bone_name):
    """Extract current pitch around world X (in degrees) of a pose bone's delta rotation."""
    pb = arm_obj.pose.bones[bone_name]
    r_local = pb.rotation_quaternion.to_matrix()
    m_rest = pb.bone.matrix_local.to_3x3()
    r_world = m_rest @ r_local @ m_rest.inverted()
    return math.degrees(r_world.to_euler('XYZ').x)


def _add_bone_world_rx_deg(arm_obj, bone_name, delta_deg):
    """Adjust a pose bone's local/pose pitch by delta_deg around armature/world X."""
    pb = arm_obj.pose.bones[bone_name]
    r_delta = Euler((math.radians(delta_deg), 0.0, 0.0), 'XYZ').to_matrix()
    m_rest = pb.bone.matrix_local.to_3x3()
    r_delta_local = m_rest.inverted() @ r_delta @ m_rest
    pb.rotation_mode = 'QUATERNION'
    pb.rotation_quaternion = (r_delta_local @ pb.rotation_quaternion.to_matrix()).to_quaternion()


def level_foot_world_horizontal(arm_obj, side, mesh_obj=None, foot_vdata=None, max_pitch_trim=6.0):
    """
    Set LeftFoot or RightFoot so its sole sits horizontal in world XY (keeping only heading yaw around Z),
    then optionally trim world pitch by <= max_pitch_trim deg so heel and forefoot vertices share the same Z.
    """
    pb = arm_obj.pose.bones[f"{side}Foot"]
    if pb.parent is None:
        return
    bpy.context.view_layer.update()

    m_parent_pose = pb.parent.matrix.to_3x3()
    m_foot_rest = pb.bone.matrix_local.to_3x3()
    m_parent_rest = pb.parent.bone.matrix_local.to_3x3()
    m_rel_rest = m_parent_rest.inverted() @ m_foot_rest

    # Measure current horizontal heading yaw (rest vector -> current vector projected on XY)
    rest_fwd = pb.bone.tail_local - pb.bone.head_local
    rest_xy = Vector((rest_fwd.x, rest_fwd.y))
    cur_fwd = pb.tail - pb.head
    cur_xy = Vector((cur_fwd.x, cur_fwd.y))
    if rest_xy.length > 1e-4 and cur_xy.length > 1e-4:
        # 2D signed angle from rest_xy to cur_xy around +Z
        a1 = math.atan2(rest_xy.y, rest_xy.x)
        a2 = math.atan2(cur_xy.y, cur_xy.x)
        yaw_rad = a2 - a1
    else:
        yaw_rad = 0.0

    r_yaw = Euler((0.0, 0.0, yaw_rad), 'XYZ').to_matrix()
    target_foot_world = r_yaw @ m_foot_rest
    q_foot = ((m_parent_pose @ m_rel_rest).inverted() @ target_foot_world).to_quaternion()
    pb.rotation_mode = 'QUATERNION'
    pb.rotation_quaternion = q_foot
    bpy.context.view_layer.update()

    # Vertex-level sole flatness polish (compensates slight heel/arch slope in source mesh)
    if mesh_obj is not None and foot_vdata and foot_vdata[side]["heel"] and foot_vdata[side]["toe"]:
        heel_vs = foot_vdata[side]["heel"]
        toe_vs = foot_vdata[side]["toe"]
        total_trim = 0.0
        for _ in range(4):
            hz = _eval_mesh_min_z(mesh_obj, heel_vs)
            tz = _eval_mesh_min_z(mesh_obj, toe_vs)
            diff = hz - tz  # positive => heel is higher than toes => pitch toes UP (negative rot around +X relative to -Y forward)
            if abs(diff) < 0.0005:
                break
            # Foot length ~0.18 m => asin(diff / 0.16)
            trim_deg = max(-max_pitch_trim, min(max_pitch_trim, -math.degrees(math.atan2(diff, 0.16))))
            trim_deg = max(-max_pitch_trim - total_trim, min(max_pitch_trim - total_trim, trim_deg))
            if abs(trim_deg) < 0.05:
                break
            total_trim += trim_deg
            r_pitch = Euler((math.radians(trim_deg), 0.0, 0.0), 'XYZ').to_matrix()
            # Apply pitch around the foot's horizontal heading axis (r_yaw @ X)
            r_trim_world = r_yaw @ r_pitch @ r_yaw.inverted()
            q_foot = ((m_parent_pose @ m_rel_rest).inverted() @ (r_trim_world @ target_foot_world)).to_quaternion()
            pb.rotation_quaternion = q_foot
            target_foot_world = r_trim_world @ target_foot_world
            bpy.context.view_layer.update()


def _get_segment_world_pitch_deg(arm_obj, bone_name):
    """World pitch (degrees) of bone segment (tail - head) from straight-down (-Z) toward back (+Y) vs front (-Y)."""
    pb = arm_obj.pose.bones[bone_name]
    v = pb.tail - pb.head
    return math.degrees(math.atan2(v.y, -v.z))


def _get_knee_flex_deg(arm_obj, side):
    """
    Anatomical sagittal knee flexion in degrees (shin_pitch - thigh_pitch):
    >= 0 means knee bends naturally backward; < 0 indicates inverted hyperextension.
    """
    thigh_pitch = _get_segment_world_pitch_deg(arm_obj, f"{side}UpperLeg")
    shin_pitch = _get_segment_world_pitch_deg(arm_obj, f"{side}LowerLeg")
    return shin_pitch - thigh_pitch


def _equalize_stance_feet(arm_obj, mesh_obj, foot_vdata):
    """
    Level both soles horizontally and balance L/R shoe Z while preserving natural anatomical knee flexion
    (knee_flex >= MIN_KNEE_FLEX) on both legs and keeping both feet grounded at the same floor level.
    """
    if mesh_obj is None or not foot_vdata:
        return
    left_vs = foot_vdata["Left"]["all"]
    right_vs = foot_vdata["Right"]["all"]
    if not left_vs or not right_vs:
        return

    MIN_KNEE_FLEX = 4.0

    # Clamp any starting negative/near-zero knee flexion before equalizing soles
    for side in ("Left", "Right"):
        kf = _get_knee_flex_deg(arm_obj, side)
        if kf < MIN_KNEE_FLEX:
            _add_bone_world_rx_deg(arm_obj, f"{side}LowerLeg", MIN_KNEE_FLEX - kf)

    level_foot_world_horizontal(arm_obj, "Left", mesh_obj, foot_vdata)
    level_foot_world_horizontal(arm_obj, "Right", mesh_obj, foot_vdata)

    for _ in range(24):
        lz = _eval_mesh_min_z(mesh_obj, left_vs)
        rz = _eval_mesh_min_z(mesh_obj, right_vs)
        diff = lz - rz
        if abs(diff) <= 0.0005:
            break

        step_deg = min(8.0, max(0.35, abs(diff) * 280.0))
        left_flex = _get_knee_flex_deg(arm_obj, "Left")
        right_flex = _get_knee_flex_deg(arm_obj, "Right")
        left_shin_pitch = _get_segment_world_pitch_deg(arm_obj, "LeftLowerLeg")
        left_thigh_pitch = _get_segment_world_pitch_deg(arm_obj, "LeftUpperLeg")
        right_thigh_pitch = _get_segment_world_pitch_deg(arm_obj, "RightUpperLeg")

        if diff > 0:
            # Left foot is higher than Right foot: extend Left knee safely above MIN_KNEE_FLEX
            # or flex Right knee gently to bring Right sole up to Left sole.
            if left_flex > MIN_KNEE_FLEX + 1.5 and left_shin_pitch > 3.5:
                ext = min(step_deg * 0.85, max(0.0, left_flex - MIN_KNEE_FLEX))
                if ext > 0.05:
                    _add_bone_world_rx_deg(arm_obj, "LeftLowerLeg", -ext)
            else:
                _add_bone_world_rx_deg(arm_obj, "RightLowerLeg", step_deg * 0.85)
                if right_flex > 22.0:
                    _add_bone_world_rx_deg(arm_obj, "RightUpperLeg", -step_deg * 0.30)
                if left_flex > 26.0 and left_thigh_pitch < -14.0:
                    _add_bone_world_rx_deg(arm_obj, "LeftUpperLeg", min(step_deg * 0.20, (-12.0) - left_thigh_pitch))
        else:
            # Right foot is higher than Left foot: extend Right knee gently without crossing MIN_KNEE_FLEX,
            # then bring RightUpperLeg closer to vertical, and flex Left leg cleanly to drop Hips.
            if right_flex > MIN_KNEE_FLEX + 1.5:
                ext = min(step_deg * 0.85, max(0.0, right_flex - MIN_KNEE_FLEX))
                if ext > 0.05:
                    _add_bone_world_rx_deg(arm_obj, "RightLowerLeg", -ext)
                if right_thigh_pitch > 8.0:
                    _add_bone_world_rx_deg(arm_obj, "RightUpperLeg", -min(step_deg * 0.20, right_thigh_pitch - 6.0))
            else:
                if right_thigh_pitch > 5.0:
                    _add_bone_world_rx_deg(arm_obj, "RightUpperLeg", -min(step_deg * 0.70, right_thigh_pitch - 4.0))
                # Flex Left hip & knee equally so shin stays near vertical while Hips lowers cleanly
                flex_step = step_deg * 0.50
                _add_bone_world_rx_deg(arm_obj, "LeftUpperLeg", -flex_step)
                _add_bone_world_rx_deg(arm_obj, "LeftLowerLeg", flex_step)

        # Enforce hard anatomical knee flexion floor on both legs after each step
        for side in ("Left", "Right"):
            kf = _get_knee_flex_deg(arm_obj, side)
            if kf < MIN_KNEE_FLEX:
                _add_bone_world_rx_deg(arm_obj, f"{side}LowerLeg", MIN_KNEE_FLEX - kf)

        level_foot_world_horizontal(arm_obj, "Left", mesh_obj, foot_vdata)
        level_foot_world_horizontal(arm_obj, "Right", mesh_obj, foot_vdata)


def ground_pose_to_floor(arm_obj, mesh_obj, foot_vdata=None, mode="both", target_z=0.0):
    """
    Ground current pose so supporting foot (or lowest mesh point) rests on target_z (default Z=0.0).
    mode:
      - 'both': level both feet horizontally, equalize L/R shoe sole Z, shift Hips Z so soles sit at target_z.
      - 'right': level RightFoot horizontally, shift Hips Z so RightFoot sole sits at target_z (Left leg kicking).
      - 'left': level LeftFoot horizontally, shift Hips Z so LeftFoot sole sits at target_z (Right leg kicking).
      - 'min': keep current leg angles (e.g. KnockDown / Walk) and shift Hips Z so min(all mesh verts) == target_z.
    """
    if mesh_obj is None:
        return 0.0
    bpy.context.view_layer.update()

    if mode == "both":
        _equalize_stance_feet(arm_obj, mesh_obj, foot_vdata)
        vs = (foot_vdata["Left"]["all"] + foot_vdata["Right"]["all"]) if foot_vdata else None
        cur_min_z = _eval_mesh_min_z(mesh_obj, vs)
    elif mode == "right":
        level_foot_world_horizontal(arm_obj, "Right", mesh_obj, foot_vdata)
        vs = foot_vdata["Right"]["all"] if (foot_vdata and foot_vdata["Right"]["all"]) else None
        cur_min_z = _eval_mesh_min_z(mesh_obj, vs)
    elif mode == "left":
        level_foot_world_horizontal(arm_obj, "Left", mesh_obj, foot_vdata)
        vs = foot_vdata["Left"]["all"] if (foot_vdata and foot_vdata["Left"]["all"]) else None
        cur_min_z = _eval_mesh_min_z(mesh_obj, vs)
    else:
        cur_min_z = _eval_mesh_min_z(mesh_obj, None)

    dz = target_z - cur_min_z
    pb_hips = arm_obj.pose.bones["Hips"]
    m_rest = pb_hips.bone.matrix_local.to_3x3()
    pb_hips.location = pb_hips.location + (m_rest.inverted() @ Vector((0.0, 0.0, dz)))
    bpy.context.view_layer.update()
    return _eval_mesh_min_z(mesh_obj, None)


def build_action_from_keyframes(arm_obj, action_name, keyframes, mesh_obj=None, foot_vdata=None, frame_ground_modes=None):
    """Create or replace an Action on arm_obj from {frame: pose_dict}, with optional per-frame vertex grounding."""
    if action_name in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions[action_name])
    act = bpy.data.actions.new(name=action_name)
    act.use_fake_user = True
    if not arm_obj.animation_data:
        arm_obj.animation_data_create()
    arm_obj.animation_data.action = act

    modes = frame_ground_modes or {}
    for frame in sorted(keyframes.keys()):
        apply_pose_dict(arm_obj, keyframes[frame])
        gmode = modes.get(frame)
        if gmode:
            ground_pose_to_floor(arm_obj, mesh_obj, foot_vdata=foot_vdata, mode=gmode, target_z=0.0)
        key_all_deform_bones(arm_obj, frame)

    # Set smooth Bezier interpolation on all created fcurves
    if act.layers and act.slots:
        cb = act.layers[0].strips[0].channelbag(act.slots[0])
        for fc in cb.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = 'BEZIER'
                kp.handle_left_type = 'AUTO_CLAMPED'
                kp.handle_right_type = 'AUTO_CLAMPED'
    return act


# ---------------------------------------------------------------------------
# Fighting Stance Base Pose & Modular Style/Weight Animation Generators (60 FPS)
# ---------------------------------------------------------------------------

def merge_pose(base, overrides):
    p = dict(base)
    p.update(overrides)
    return p


def get_idle_stance(breath=0.0, style="heavy_brawler"):
    """
    Balanced fighting stance facing -Y (side-on to 2.5D camera) customized per fighting_style.
    Hips keep rx = 0.0 so the pelvis/torso stay upright, Spine+Chest+UpperChest sum stays in 2-6 deg,
    front LeftFoot stays clearly ahead of Hips (shin near vertical), and RightFoot sits behind Hips.
    """
    if style == "street_scrappy":
        # Tiago: open asymmetrical street stance, looser/lower hands, upright street balance
        return {
            "HipsOffset":    (0.0, 0.0, -0.036),
            "Hips":          (0.0, 1.2 * breath, 18.0 + 2.0 * breath),
            "Spine":         (2.0 + 1.4 * breath, 2.0 * breath, 3.0 - 2.5 * breath),
            "Chest":         (1.5 + 1.0 * breath, 1.5 * breath, 3.0),
            "UpperChest":    (1.0, 0.0, 2.0),
            "Neck":          (-2.5 - 0.8 * breath, 0.0, -8.0),
            "Head":          (-2.0, 0.0, -10.0),
            # Lead left arm relaxed/lower in front (not tight boxing guard)
            "LeftShoulder":  (0.0, 8.0, -8.0),
            "LeftUpperArm":  (-4.0, 62.0 - 4.0 * breath, -42.0 + 4.0 * breath),
            "LeftLowerArm":  (6.0, -18.0, -64.0),
            "LeftHand":      (-6.0, 0.0, 6.0),
            # Rear right arm loose near chest/ribcage
            "RightShoulder": (0.0, -8.0, 8.0),
            "RightUpperArm": (-3.0, -60.0 + 4.0 * breath, 34.0 - 3.0 * breath),
            "RightLowerArm": (8.0, 22.0, 74.0),
            "RightHand":     (-8.0, 0.0, -8.0),
            # Balanced stride: front shin near vertical, rear foot moderately back (auto-leveled to Z=0)
            "LeftUpperLeg":  (-27.0, -6.0, 6.0),
            "LeftLowerLeg":  (8.0, 0.0, 0.0),
            "LeftFoot":      (19.0, 0.0, 0.0),
            "RightUpperLeg": (15.0, 6.0, -5.0),
            "RightLowerLeg": (7.0, 0.0, 0.0),
            "RightFoot":     (-22.0, 0.0, 0.0),
        }

    if style == "technical_striker":
        # Jompes (Shadow Sentinel): disciplined martial-arts stance, high lead guard & balanced center of gravity
        return {
            "HipsOffset":    (0.0, 0.0, -0.036),
            "Hips":          (0.0, 0.0, 26.0),
            "Spine":         (1.5 + 0.8 * breath, 0.0, 4.0),
            "Chest":         (1.5 + 0.8 * breath, 0.0, 3.0),
            "UpperChest":    (1.0, 0.0, 2.0),
            "Neck":          (-2.5 - 0.6 * breath, 0.0, -18.0),
            "Head":          (-2.0, 0.0, -16.0),
            # Lead left hand high, angled forward like karate/kickboxing sentinel guard
            "LeftShoulder":  (0.0, 14.0, -14.0),
            "LeftUpperArm":  (-12.0, 42.0 - 1.8 * breath, -52.0),
            "LeftLowerArm":  (12.0, -32.0, -96.0),
            "LeftHand":      (-10.0, 0.0, 12.0),
            # Rear right hand tightly tucked near chin/chest
            "RightShoulder": (0.0, -14.0, 14.0),
            "RightUpperArm": (-6.0, -42.0 + 1.8 * breath, 66.0),
            "RightLowerArm": (14.0, 42.0, 118.0),
            "RightHand":     (-12.0, 0.0, -12.0),
            # Compact grounded martial stance (front shin near vertical, auto-leveled to Z=0)
            "LeftUpperLeg":  (-28.0, -5.0, 5.0),
            "LeftLowerLeg":  (8.0, 0.0, 0.0),
            "LeftFoot":      (20.0, 0.0, 0.0),
            "RightUpperLeg": (15.0, 5.0, -5.0),
            "RightLowerLeg": (8.0, 0.0, 0.0),
            "RightFoot":     (-23.0, 0.0, 0.0),
        }

    if style == "agile_striker":
        # Rochard (Vasco Jacket): athletic agile guard ginga centered between both feet
        return {
            "HipsOffset":    (0.0, 0.0, -0.032),
            "Hips":          (0.0, 1.2 * breath, 22.0 + 2.5 * breath),
            "Spine":         (2.0 + 1.4 * breath, -1.5 * breath, 4.0),
            "Chest":         (2.0 + 1.4 * breath, -1.2 * breath, 4.0),
            "UpperChest":    (1.0, 0.0, 2.0),
            "Neck":          (-2.5 - 0.8 * breath, 0.0, -14.0),
            "Head":          (-2.0, 0.0, -14.0),
            "LeftShoulder":  (0.0, 12.0, -12.0),
            "LeftUpperArm":  (-8.0, 52.0 - 4.0 * breath, -56.0 + 4.0 * breath),
            "LeftLowerArm":  (12.0, -32.0, -96.0),
            "LeftHand":      (-10.0, 0.0, 10.0),
            "RightShoulder": (0.0, -12.0, 12.0),
            "RightUpperArm": (-8.0, -52.0 + 4.0 * breath, 56.0 - 4.0 * breath),
            "RightLowerArm": (10.0, 34.0, 104.0),
            "RightHand":     (-10.0, 0.0, -10.0),
            "LeftUpperLeg":  (-27.0, -4.0, 4.0),
            "LeftLowerLeg":  (8.0, 0.0, 0.0),
            "LeftFoot":      (19.0, 0.0, 0.0),
            "RightUpperLeg": (15.0, 5.0, -4.0),
            "RightLowerLeg": (8.0, 0.0, 0.0),
            "RightFoot":     (-23.0, 0.0, 0.0),
        }

    # Default: heavy_brawler (Lucas): firm planted heavy stance, upright spine & tight temple/chin guard
    return {
        "HipsOffset":    (0.0, 0.0, -0.045),
        "Hips":          (0.0, 0.0, 22.0),
        "Spine":         (1.0 + 1.2 * breath, 0.0, 4.0),
        "Chest":         (2.0 + 1.2 * breath, 0.0, 4.0),
        "UpperChest":    (1.5, 0.0, 2.5),
        "Neck":          (-3.0 - 0.8 * breath, 0.0, -15.0),
        "Head":          (-2.5, 0.0, -15.0),
        # High closed lead guard protecting temple/cheek
        "LeftShoulder":  (0.0, 14.0, -14.0),
        "LeftUpperArm":  (-10.0, 44.0 - 2.5 * breath, -66.0),
        "LeftLowerArm":  (14.0, -38.0, -110.0),
        "LeftHand":      (-12.0, 0.0, 14.0),
        # Tight rear chin guard
        "RightShoulder": (0.0, -14.0, 14.0),
        "RightUpperArm": (-8.0, -46.0 + 2.5 * breath, 64.0),
        "RightLowerArm": (12.0, 40.0, 114.0),
        "RightHand":     (-12.0, 0.0, -14.0),
        # Planted heavy stance legs centered under pelvis (auto-leveled to Z=0)
        "LeftUpperLeg":  (-28.0, -5.0, 6.0),
        "LeftLowerLeg":  (9.0, 0.0, 0.0),
        "LeftFoot":      (19.0, 0.0, 0.0),
        "RightUpperLeg": (16.0, 6.0, -6.0),
        "RightLowerLeg": (8.0, 0.0, 0.0),
        "RightFoot":     (-24.0, 0.0, 0.0),
    }


def _attack_key_frames(startup, active, recovery):
    """Map (startup, active, recovery) ticks cleanly into 1-based Blender frames of length Total+1."""
    ant_f = max(2, 1 + round(startup * 0.6))
    hit_start = 1 + startup
    if ant_f >= hit_start:
        ant_f = max(2, hit_start - 1)
    hit_end = hit_start + active
    end_f = hit_end + recovery  # TotalTicks + 1
    return ant_f, hit_start, hit_end, end_f


def build_style_locomotion(style, idle0):
    anims = {}

    if style == "street_scrappy":
        # Loose irregular street walk, arms swing outside boxing guard
        wf_a = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.044),
            "Spine":         (3.5, 3.0, 10.0),
            "LeftUpperArm":  (-2.0, 68.0, -26.0),
            "LeftLowerArm":  (6.0, -10.0, -42.0),
            "RightUpperArm": (-6.0, -66.0, 46.0),
            "RightLowerArm": (8.0, 14.0, 56.0),
            "LeftUpperLeg":  (-30.0, -6.0, 7.0),
            "LeftLowerLeg":  (18.0, 0.0, 0.0),
            "LeftFoot":      (8.0, 0.0, 0.0),
            "RightUpperLeg": (20.0, 6.0, -6.0),
            "RightLowerLeg": (12.0, 0.0, 0.0),
            "RightFoot":     (-12.0, 0.0, 0.0),
        })
        wf_mid1 = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.025),
            "Spine":         (2.5, -2.0, 2.0),
            "LeftUpperArm":  (-5.0, 64.0, -38.0),
            "RightUpperArm": (-4.0, -62.0, 32.0),
            "LeftUpperLeg":  (-8.0, -4.0, 4.0),
            "LeftLowerLeg":  (12.0, 0.0, 0.0),
            "LeftFoot":      (-4.0, 0.0, 0.0),
            "RightUpperLeg": (-22.0, 4.0, -4.0),
            "RightLowerLeg": (46.0, 0.0, 0.0),
            "RightFoot":     (-12.0, 0.0, 0.0),
        })
        wf_b = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.044),
            "Spine":         (3.5, -3.0, -6.0),
            "LeftUpperArm":  (-6.0, 68.0, -52.0),
            "LeftLowerArm":  (8.0, -14.0, -68.0),
            "RightUpperArm": (-2.0, -68.0, 18.0),
            "RightLowerArm": (6.0, 10.0, 46.0),
            "LeftUpperLeg":  (20.0, -6.0, 7.0),
            "LeftLowerLeg":  (12.0, 0.0, 0.0),
            "LeftFoot":      (-12.0, 0.0, 0.0),
            "RightUpperLeg": (-30.0, 6.0, -6.0),
            "RightLowerLeg": (18.0, 0.0, 0.0),
            "RightFoot":     (8.0, 0.0, 0.0),
        })
        wf_mid2 = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.025),
            "Spine":         (2.5, 2.0, 4.0),
            "LeftUpperLeg":  (-22.0, -4.0, 4.0),
            "LeftLowerLeg":  (46.0, 0.0, 0.0),
            "LeftFoot":      (-12.0, 0.0, 0.0),
            "RightUpperLeg": (-8.0, 4.0, -4.0),
            "RightLowerLeg": (12.0, 0.0, 0.0),
            "RightFoot":     (-4.0, 0.0, 0.0),
        })
        wb_arm_l = {"LeftUpperArm": (-2.0, 64.0, -30.0), "RightUpperArm": (-2.0, -64.0, 26.0)}
    elif style == "technical_striker":
        # Disciplined compact martial footwork keeping high guard locked steady
        wf_a = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.042),
            "Spine":         (2.0, 0.0, 6.0),
            "LeftUpperLeg":  (-28.0, -5.0, 5.0),
            "LeftLowerLeg":  (18.0, 0.0, 0.0),
            "LeftFoot":      (6.0, 0.0, 0.0),
            "RightUpperLeg": (18.0, 5.0, -5.0),
            "RightLowerLeg": (12.0, 0.0, 0.0),
            "RightFoot":     (-12.0, 0.0, 0.0),
        })
        wf_mid1 = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.030),
            "Spine":         (1.8, 0.0, 3.0),
            "LeftUpperLeg":  (-10.0, -3.0, 3.0),
            "LeftLowerLeg":  (12.0, 0.0, 0.0),
            "LeftFoot":      (-4.0, 0.0, 0.0),
            "RightUpperLeg": (-22.0, 4.0, -3.0),
            "RightLowerLeg": (44.0, 0.0, 0.0),
            "RightFoot":     (-10.0, 0.0, 0.0),
        })
        wf_b = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.042),
            "Spine":         (2.0, 0.0, 1.0),
            "LeftUpperLeg":  (18.0, -5.0, 5.0),
            "LeftLowerLeg":  (12.0, 0.0, 0.0),
            "LeftFoot":      (-12.0, 0.0, 0.0),
            "RightUpperLeg": (-28.0, 5.0, -5.0),
            "RightLowerLeg": (18.0, 0.0, 0.0),
            "RightFoot":     (6.0, 0.0, 0.0),
        })
        wf_mid2 = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.030),
            "Spine":         (1.8, 0.0, 4.0),
            "LeftUpperLeg":  (-22.0, -4.0, 3.0),
            "LeftLowerLeg":  (44.0, 0.0, 0.0),
            "LeftFoot":      (-10.0, 0.0, 0.0),
            "RightUpperLeg": (-10.0, 3.0, -3.0),
            "RightLowerLeg": (12.0, 0.0, 0.0),
            "RightFoot":     (-4.0, 0.0, 0.0),
        })
        wb_arm_l = {}
    elif style == "agile_striker":
        # Light springy footwork with lively shoulder/torso bounce
        wf_a = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.034),
            "Spine":         (3.5, 2.0, 10.0),
            "LeftUpperArm":  (-8.0, 48.0, -54.0),
            "RightUpperArm": (-8.0, -50.0, 52.0),
            "LeftUpperLeg":  (-32.0, -4.0, 4.0),
            "LeftLowerLeg":  (18.0, 0.0, 0.0),
            "LeftFoot":      (8.0, 0.0, 0.0),
            "RightUpperLeg": (22.0, 4.0, -4.0),
            "RightLowerLeg": (14.0, 0.0, 0.0),
            "RightFoot":     (-14.0, 0.0, 0.0),
        })
        wf_mid1 = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.012),
            "Spine":         (2.5, -2.0, 2.0),
            "LeftUpperLeg":  (-8.0, -3.0, 2.0),
            "LeftLowerLeg":  (12.0, 0.0, 0.0),
            "LeftFoot":      (-4.0, 0.0, 0.0),
            "RightUpperLeg": (-26.0, 3.0, -2.0),
            "RightLowerLeg": (52.0, 0.0, 0.0),
            "RightFoot":     (-12.0, 0.0, 0.0),
        })
        wf_b = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.034),
            "Spine":         (3.5, -2.0, -4.0),
            "LeftUpperArm":  (-10.0, 54.0, -60.0),
            "RightUpperArm": (-8.0, -48.0, 58.0),
            "LeftUpperLeg":  (22.0, -4.0, 4.0),
            "LeftLowerLeg":  (14.0, 0.0, 0.0),
            "LeftFoot":      (-14.0, 0.0, 0.0),
            "RightUpperLeg": (-32.0, 4.0, -4.0),
            "RightLowerLeg": (18.0, 0.0, 0.0),
            "RightFoot":     (8.0, 0.0, 0.0),
        })
        wf_mid2 = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.012),
            "Spine":         (2.5, 2.0, 4.0),
            "LeftUpperLeg":  (-26.0, -3.0, 2.0),
            "LeftLowerLeg":  (52.0, 0.0, 0.0),
            "LeftFoot":      (-12.0, 0.0, 0.0),
            "RightUpperLeg": (-8.0, 3.0, -2.0),
            "RightLowerLeg": (12.0, 0.0, 0.0),
            "RightFoot":     (-4.0, 0.0, 0.0),
        })
        wb_arm_l = {}
    else:
        # heavy_brawler: heavy planted advance while keeping upright torso & tight high guard
        wf_a = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.055),
            "Spine":         (3.0, 0.0, 8.0),
            "LeftUpperArm":  (-10.0, 42.0, -68.0),
            "RightUpperArm": (-10.0, -44.0, 66.0),
            "LeftUpperLeg":  (-32.0, -5.0, 6.0),
            "LeftLowerLeg":  (22.0, 0.0, 0.0),
            "LeftFoot":      (8.0, 0.0, 0.0),
            "RightUpperLeg": (22.0, 5.0, -6.0),
            "RightLowerLeg": (16.0, 0.0, 0.0),
            "RightFoot":     (-14.0, 0.0, 0.0),
        })
        wf_mid1 = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.042),
            "Spine":         (2.0, 0.0, 4.0),
            "LeftUpperLeg":  (-12.0, -4.0, 4.0),
            "LeftLowerLeg":  (14.0, 0.0, 0.0),
            "LeftFoot":      (-4.0, 0.0, 0.0),
            "RightUpperLeg": (-26.0, 4.0, -4.0),
            "RightLowerLeg": (48.0, 0.0, 0.0),
            "RightFoot":     (-12.0, 0.0, 0.0),
        })
        wf_b = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.055),
            "Spine":         (3.0, 0.0, -2.0),
            "LeftUpperArm":  (-12.0, 46.0, -70.0),
            "RightUpperArm": (-8.0, -44.0, 68.0),
            "LeftUpperLeg":  (22.0, -5.0, 6.0),
            "LeftLowerLeg":  (16.0, 0.0, 0.0),
            "LeftFoot":      (-14.0, 0.0, 0.0),
            "RightUpperLeg": (-32.0, 5.0, -6.0),
            "RightLowerLeg": (22.0, 0.0, 0.0),
            "RightFoot":     (8.0, 0.0, 0.0),
        })
        wf_mid2 = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.042),
            "Spine":         (2.0, 0.0, 6.0),
            "LeftUpperLeg":  (-26.0, -4.0, 4.0),
            "LeftLowerLeg":  (48.0, 0.0, 0.0),
            "LeftFoot":      (-12.0, 0.0, 0.0),
            "RightUpperLeg": (-12.0, 4.0, -4.0),
            "RightLowerLeg": (14.0, 0.0, 0.0),
            "RightFoot":     (-4.0, 0.0, 0.0),
        })
        wb_arm_l = {}

    anims["WalkForward"] = {1: wf_a, 13: wf_mid1, 25: wf_b, 37: wf_mid2, 49: wf_a}

    wb_a = merge_pose(idle0, merge_pose({
        "HipsOffset":    (0.0, 0.0, -0.045 if style != "agile_striker" else -0.032),
        "Spine":         (1.5, 0.0, 4.0),
        "LeftUpperLeg":  (16.0, -4.0, 4.0),
        "LeftLowerLeg":  (12.0, 0.0, 0.0),
        "LeftFoot":      (-16.0, 0.0, 0.0),
        "RightUpperLeg": (-24.0, 4.0, -4.0),
        "RightLowerLeg": (18.0, 0.0, 0.0),
        "RightFoot":     (4.0, 0.0, 0.0),
    }, wb_arm_l))
    wb_mid1 = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.030),
        "LeftUpperLeg":  (-18.0, -3.0, 2.0),
        "LeftLowerLeg":  (40.0, 0.0, 0.0),
        "RightUpperLeg": (-10.0, 3.0, -2.0),
        "RightLowerLeg": (12.0, 0.0, 0.0),
    })
    wb_b = merge_pose(idle0, merge_pose({
        "HipsOffset":    (0.0, 0.0, -0.045 if style != "agile_striker" else -0.032),
        "Spine":         (1.5, 0.0, 6.0),
        "LeftUpperLeg":  (-24.0, -4.0, 4.0),
        "LeftLowerLeg":  (18.0, 0.0, 0.0),
        "LeftFoot":      (4.0, 0.0, 0.0),
        "RightUpperLeg": (16.0, 4.0, -4.0),
        "RightLowerLeg": (12.0, 0.0, 0.0),
        "RightFoot":     (-16.0, 0.0, 0.0),
    }, wb_arm_l))
    wb_mid2 = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.035),
        "LeftUpperLeg":  (-10.0, -3.0, 2.0),
        "LeftLowerLeg":  (14.0, 0.0, 0.0),
        "RightUpperLeg": (-18.0, 3.0, -2.0),
        "RightLowerLeg": (46.0, 0.0, 0.0),
    })
    anims["WalkBackward"] = {1: wb_a, 13: wb_mid1, 25: wb_b, 37: wb_mid2, 49: wb_a}
    return anims


def build_style_block_and_victory(style, idle0):
    anims = {}

    if style == "street_scrappy":
        # Instinctive street block: shrug shoulders up, duck head, cross forearms across face
        block_pose = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.048),
            "Spine":         (4.5, 0.0, 4.0),
            "Chest":         (4.0, 0.0, 2.0),
            "Neck":          (8.0, 0.0, -8.0),
            "Head":          (6.0, 0.0, -6.0),
            "LeftShoulder":  (0.0, -10.0, -18.0),
            "LeftUpperArm":  (-16.0, 26.0, -78.0),
            "LeftLowerArm":  (22.0, -52.0, -124.0),
            "LeftHand":      (-14.0, 12.0, 18.0),
            "RightShoulder": (0.0, 10.0, 18.0),
            "RightUpperArm": (-18.0, -28.0, 76.0),
            "RightLowerArm": (22.0, 52.0, 124.0),
            "RightHand":     (-14.0, -12.0, -18.0),
        })
        # Casual street celebration: upright hip posture, raise left arm angled out & chill right arm
        vic_pose = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.01),
            "Hips":          (0.0, -2.0, 16.0),
            "Spine":         (-3.0, -3.0, 2.0),
            "Chest":         (-2.0, 0.0, 3.0),
            "Neck":          (-3.0, 0.0, -8.0),
            "Head":          (-2.0, 0.0, -6.0),
            "LeftShoulder":  (0.0, -14.0, -10.0),
            "LeftUpperArm":  (-12.0, -42.0, -34.0),
            "LeftLowerArm":  (0.0, -10.0, -38.0),
            "LeftHand":      (0.0, 0.0, 0.0),
            "RightShoulder": (0.0, -8.0, 10.0),
            "RightUpperArm": (0.0, -70.0, 18.0),
            "RightLowerArm": (0.0, 12.0, 44.0),
            "RightHand":     (0.0, 0.0, 0.0),
            "LeftUpperLeg":  (-16.0, -5.0, 6.0),
            "LeftLowerLeg":  (10.0, 0.0, 0.0),
            "LeftFoot":      (6.0, 0.0, 0.0),
            "RightUpperLeg": (12.0, 5.0, -6.0),
            "RightLowerLeg": (4.0, 0.0, 0.0),
            "RightFoot":     (-16.0, 0.0, 0.0),
        })
    elif style == "technical_striker":
        # Disciplined martial peek-a-boo / tight elbow block & firm sentinel victory posture
        block_pose = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.045),
            "Spine":         (3.0, 0.0, 2.0),
            "Chest":         (3.0, 0.0, 2.0),
            "Neck":          (-4.0, 0.0, -14.0),
            "LeftShoulder":  (0.0, 18.0, -18.0),
            "LeftUpperArm":  (-16.0, 36.0, -72.0),
            "LeftLowerArm":  (20.0, -48.0, -120.0),
            "LeftHand":      (-14.0, 8.0, 14.0),
            "RightShoulder": (0.0, -18.0, 18.0),
            "RightUpperArm": (-16.0, -36.0, 72.0),
            "RightLowerArm": (20.0, 48.0, 120.0),
            "RightHand":     (-14.0, -8.0, -14.0),
        })
        vic_pose = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.015),
            "Hips":          (0.0, 0.0, 18.0),
            "Spine":         (-2.0, 0.0, 2.0),
            "Chest":         (-2.0, 0.0, 2.0),
            "Neck":          (-3.0, 0.0, -16.0),
            "Head":          (-2.0, 0.0, -10.0),
            # Left forearm crisp across upper chest / martial guard, right fist chambered firmly at waist/ribs
            "LeftShoulder":  (0.0, 14.0, -10.0),
            "LeftUpperArm":  (-12.0, 46.0, -48.0),
            "LeftLowerArm":  (14.0, -36.0, -108.0),
            "LeftHand":      (-8.0, 0.0, 10.0),
            "RightShoulder": (0.0, -10.0, 8.0),
            "RightUpperArm": (14.0, -56.0, 28.0),
            "RightLowerArm": (10.0, 28.0, 96.0),
            "RightHand":     (0.0, 0.0, 0.0),
            "LeftUpperLeg":  (-14.0, -4.0, 4.0),
            "LeftLowerLeg":  (10.0, 0.0, 0.0),
            "LeftFoot":      (4.0, 0.0, 0.0),
            "RightUpperLeg": (10.0, 4.0, -4.0),
            "RightLowerLeg": (4.0, 0.0, 0.0),
            "RightFoot":     (-14.0, 0.0, 0.0),
        })
    elif style == "agile_striker":
        block_pose = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.042),
            "Spine":         (3.5, 0.0, 2.0),
            "Chest":         (3.5, 0.0, 2.0),
            "Neck":          (-5.0, 0.0, -12.0),
            "LeftShoulder":  (0.0, 16.0, -16.0),
            "LeftUpperArm":  (-14.0, 40.0, -70.0),
            "LeftLowerArm":  (18.0, -46.0, -114.0),
            "RightShoulder": (0.0, -16.0, 16.0),
            "RightUpperArm": (-14.0, -40.0, 70.0),
            "RightLowerArm": (18.0, 46.0, 114.0),
        })
        # Energetic, vibrant high-pump victory pose
        vic_pose = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, 0.005),
            "Hips":          (0.0, -2.0, 14.0),
            "Spine":         (-3.0, 0.0, 2.0),
            "Chest":         (-4.0, 0.0, 2.0),
            "Neck":          (-3.0, 0.0, -10.0),
            "Head":          (-3.0, 0.0, -6.0),
            "RightShoulder": (0.0, 18.0, 14.0),
            "RightUpperArm": (-24.0, 52.0, 26.0),
            "RightLowerArm": (0.0, 12.0, 48.0),
            "RightHand":     (0.0, 0.0, 0.0),
            "LeftShoulder":  (0.0, -12.0, -12.0),
            "LeftUpperArm":  (-16.0, 60.0, -26.0),
            "LeftLowerArm":  (10.0, -24.0, -72.0),
            "LeftUpperLeg":  (-16.0, -4.0, 4.0),
            "LeftLowerLeg":  (10.0, 0.0, 0.0),
            "LeftFoot":      (6.0, 0.0, 0.0),
            "RightUpperLeg": (12.0, 4.0, -4.0),
            "RightLowerLeg": (4.0, 0.0, 0.0),
            "RightFoot":     (-16.0, 0.0, 0.0),
        })
    else:
        # heavy_brawler (Lucas): rock-solid closed double guard & dominant raised fist victory
        block_pose = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.056),
            "Spine":         (3.0, 0.0, 2.0),
            "Chest":         (4.0, 0.0, 2.0),
            "Neck":          (-5.0, 0.0, -12.0),
            "LeftShoulder":  (0.0, 18.0, -18.0),
            "LeftUpperArm":  (-14.0, 38.0, -72.0),
            "LeftLowerArm":  (20.0, -48.0, -118.0),
            "LeftHand":      (-18.0, 10.0, 15.0),
            "RightShoulder": (0.0, -18.0, 18.0),
            "RightUpperArm": (-14.0, -38.0, 72.0),
            "RightLowerArm": (20.0, 48.0, 118.0),
            "RightHand":     (-18.0, -10.0, -15.0),
        })
        vic_pose = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.015),
            "Hips":          (0.0, 0.0, 16.0),
            "Spine":         (-3.0, 0.0, 4.0),
            "Chest":         (-3.0, 0.0, 4.0),
            "Neck":          (-4.0, 0.0, -12.0),
            "Head":          (-3.0, 0.0, -4.0),
            "RightShoulder": (0.0, 16.0, 8.0),
            "RightUpperArm": (-12.0, 42.0, 16.0),
            "RightLowerArm": (0.0, 10.0, 32.0),
            "RightHand":     (0.0, 0.0, 0.0),
            "LeftUpperArm":  (-8.0, 56.0, -38.0),
            "LeftLowerArm":  (12.0, -30.0, -90.0),
            "LeftUpperLeg":  (-16.0, -5.0, 5.0),
            "LeftLowerLeg":  (12.0, 0.0, 0.0),
            "LeftFoot":      (4.0, 0.0, 0.0),
            "RightUpperLeg": (12.0, 5.0, -5.0),
            "RightLowerLeg": (4.0, 0.0, 0.0),
            "RightFoot":     (-16.0, 0.0, 0.0),
        })

    anims["BlockStand"] = {1: idle0, 4: block_pose, 25: block_pose}
    anims["Victory"] = {1: idle0, 16: vic_pose, 61: vic_pose}
    return anims


def build_style_attacks(style, idle0, framedata):
    anims = {}

    if style == "street_scrappy":
        # LightPunch: hurried slightly crooked street jab, flared elbow, dropped rear guard
        lp_ant = merge_pose(idle0, {
            "Hips":          (0.0, 0.0, 22.0),
            "Spine":         (2.5, 0.0, 6.0),
            "LeftUpperArm":  (-4.0, 60.0, -34.0),
            "LeftLowerArm":  (8.0, -24.0, -96.0),
        })
        lp_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.042),
            "Hips":          (1.0, 0.0, 4.0),
            "Spine":         (4.0, 4.0, -10.0),
            "Chest":         (3.5, 3.0, -14.0),
            "Neck":          (-4.0, 0.0, 12.0),
            "Head":          (-3.0, 0.0, 12.0),
            "LeftShoulder":  (0.0, 6.0, -14.0),
            "LeftUpperArm":  (-14.0, 16.0, -56.0),
            "LeftLowerArm":  (8.0, -16.0, -14.0),
            "LeftHand":      (-12.0, -22.0, 0.0),
            "RightUpperArm": (-4.0, -68.0, 22.0),
            "RightLowerArm": (8.0, 18.0, 62.0),
        })

        # HeavyPunch: wide open "mata-cobra" overhand swing with dynamic reach without tipping forward
        hp_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.048),
            "Hips":          (2.0, -6.0, -24.0),
            "Spine":         (3.0, -8.0, -18.0),
            "Chest":         (3.0, -8.0, -20.0),
            "Neck":          (-3.0, 0.0, 22.0),
            "RightShoulder": (0.0, -10.0, -22.0),
            "RightUpperArm": (14.0, -12.0, -34.0),
            "RightLowerArm": (0.0, 10.0, 42.0),
            "RightHand":     (-12.0, 10.0, 0.0),
        })
        hp_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.056),
            "Hips":          (3.0, 3.0, 16.0),
            "Spine":         (5.0, 3.0, 14.0),
            "Chest":         (5.0, 3.0, 16.0),
            "UpperChest":    (3.0, 2.0, 10.0),
            "Neck":          (-5.0, -4.0, -22.0),
            "Head":          (-4.0, -3.0, -20.0),
            "RightShoulder": (0.0, 10.0, 14.0),
            "RightUpperArm": (-18.0, -6.0, 22.0),
            "RightLowerArm": (4.0, 34.0, 14.0),
            "RightHand":     (-18.0, 32.0, 0.0),
            "LeftShoulder":  (0.0, 10.0, -8.0),
            "LeftUpperArm":  (-8.0, 64.0, -28.0),
            "LeftLowerArm":  (8.0, -16.0, -64.0),
            "LeftUpperLeg":  (-30.0, -5.0, 6.0),
            "LeftLowerLeg":  (20.0, 0.0, 0.0),
            "RightUpperLeg": (14.0, 6.0, -5.0),
            "RightLowerLeg": (10.0, 0.0, 0.0),
        })

        # LightKick: scrappy improvised low toe punt / street shin kick
        lk_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.02),
            "Hips":          (-4.0, 2.0, 12.0),
            "LeftUpperLeg":  (-62.0, -4.0, 6.0),
            "LeftLowerLeg":  (82.0, 0.0, 0.0),
        })
        lk_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.02),
            "Hips":          (-8.0, 3.0, 10.0),
            "Spine":         (-5.0, -4.0, 4.0),
            "LeftUpperArm":  (-8.0, 46.0, -48.0),
            "RightUpperArm": (-6.0, -48.0, 36.0),
            "LeftUpperLeg":  (-52.0, 4.0, 6.0),
            "LeftLowerLeg":  (12.0, 0.0, 0.0),
            "LeftFoot":      (28.0, 0.0, 0.0),
            "RightUpperLeg": (8.0, 4.0, -4.0),
            "RightLowerLeg": (14.0, 0.0, 0.0),
            "RightFoot":     (-12.0, 0.0, 0.0),
        })

        # HeavyKick: awkward high street kick throwing opposite arm up for balance
        hk_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.03),
            "Hips":          (-10.0, -4.0, -8.0),
            "Spine":         (-8.0, -6.0, 6.0),
            "RightUpperArm": (-8.0, 18.0, 32.0),
            "LeftUpperLeg":  (-84.0, -6.0, 8.0),
            "LeftLowerLeg":  (98.0, 0.0, 0.0),
        })
        hk_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.02),
            "Hips":          (-16.0, -8.0, -10.0),
            "Spine":         (-12.0, -10.0, 8.0),
            "Chest":         (4.0, -6.0, 8.0),
            "Neck":          (10.0, 0.0, 4.0),
            "RightShoulder": (0.0, 14.0, 12.0),
            "RightUpperArm": (-12.0, 38.0, 26.0),
            "RightLowerArm": (0.0, 16.0, 34.0),
            "LeftUpperArm":  (-10.0, 36.0, -54.0),
            "LeftUpperLeg":  (-98.0, -6.0, 10.0),
            "LeftLowerLeg":  (10.0, 0.0, 0.0),
            "LeftFoot":      (24.0, 0.0, 0.0),
            "RightUpperLeg": (10.0, -6.0, 8.0),
            "RightLowerLeg": (16.0, 0.0, 0.0),
            "RightFoot":     (-12.0, 0.0, 0.0),
        })

        # Special (Sequência de Rua — Tiago): fast stepping lead hook into heavy downward street haymaker
        sp_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.042),
            "Hips":          (2.0, -3.0, -16.0),
            "Spine":         (4.0, -4.0, -12.0),
            "Chest":         (4.0, -4.0, -12.0),
            "LeftUpperArm":  (-6.0, 54.0, -28.0),
            "LeftLowerArm":  (10.0, -22.0, -88.0),
            "RightUpperArm": (10.0, -42.0, 26.0),
            "RightLowerArm": (8.0, 24.0, 82.0),
        })
        sp_hit1 = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.046),
            "Hips":          (2.5, 3.0, 10.0),
            "Spine":         (5.0, 3.0, 14.0),
            "Chest":         (4.0, 3.0, 16.0),
            "UpperChest":    (2.0, 1.0, 6.0),
            "Neck":          (-4.0, 0.0, -18.0),
            "LeftShoulder":  (0.0, 12.0, -18.0),
            "LeftUpperArm":  (-18.0, 12.0, -56.0),
            "LeftLowerArm":  (10.0, -22.0, -42.0),
            "LeftHand":      (-14.0, -24.0, 0.0),
            "RightUpperArm": (14.0, -22.0, -28.0),
            "RightLowerArm": (6.0, 14.0, 56.0),
            "LeftUpperLeg":  (-28.0, -6.0, 6.0),
            "LeftLowerLeg":  (20.0, 0.0, 0.0),
            "RightUpperLeg": (14.0, 6.0, -5.0),
            "RightLowerLeg": (10.0, 0.0, 0.0),
        })
        sp_hit2 = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.052),
            "Hips":          (3.5, 3.0, 18.0),
            "Spine":         (5.5, 4.0, 16.0),
            "Chest":         (5.0, 3.0, 18.0),
            "UpperChest":    (3.5, 2.0, 10.0),
            "Neck":          (-6.0, -4.0, -26.0),
            "Head":          (-5.0, -3.0, -24.0),
            "RightShoulder": (0.0, 12.0, 16.0),
            "RightUpperArm": (-22.0, -8.0, 20.0),
            "RightLowerArm": (6.0, 34.0, 12.0),
            "RightHand":     (-18.0, 34.0, 0.0),
            "LeftUpperArm":  (-6.0, 60.0, -24.0),
            "LeftLowerArm":  (10.0, -24.0, -86.0),
            "LeftUpperLeg":  (-30.0, -5.0, 6.0),
            "LeftLowerLeg":  (20.0, 0.0, 0.0),
            "RightUpperLeg": (14.0, 6.0, -5.0),
            "RightLowerLeg": (10.0, 0.0, 0.0),
        })
        special_builder = ("multi", sp_ant, sp_hit1, sp_hit2)

    elif style == "technical_striker":
        # Jompes (Shadow Sentinel): crisp disciplined martial-arts strikes with clean hip rotation
        lp_ant = merge_pose(idle0, {
            "Hips":          (0.0, 0.0, 36.0),
            "Spine":         (2.0, 0.0, 6.0),
            "LeftUpperArm":  (-8.0, 44.0, -48.0),
            "LeftLowerArm":  (12.0, -34.0, -104.0),
        })
        lp_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.032),
            "Hips":          (1.0, 0.0, 4.0),
            "Spine":         (2.5, 0.0, -12.0),
            "Chest":         (2.5, 0.0, -14.0),
            "Neck":          (-3.0, 0.0, 14.0),
            "Head":          (-3.0, 0.0, 14.0),
            "LeftShoulder":  (0.0, 10.0, -20.0),
            "LeftUpperArm":  (-10.0, 2.0, -64.0),
            "LeftLowerArm":  (0.0, -28.0, -3.0),
            "LeftHand":      (-18.0, -32.0, 0.0),
            "RightUpperArm": (-6.0, -44.0, 68.0),
            "RightLowerArm": (14.0, 42.0, 118.0),
        })
        hp_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.040),
            "Hips":          (2.0, 0.0, -14.0),
            "Spine":         (2.5, 0.0, -14.0),
            "Chest":         (2.5, 0.0, -16.0),
            "Neck":          (-3.0, 0.0, 18.0),
            "RightShoulder": (0.0, -10.0, -12.0),
            "RightUpperArm": (6.0, -42.0, 18.0),
            "RightLowerArm": (10.0, 28.0, 110.0),
        })
        hp_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.044),
            "Hips":          (2.5, 0.0, 26.0),
            "Spine":         (3.5, 0.0, 16.0),
            "Chest":         (3.5, 0.0, 18.0),
            "UpperChest":    (2.5, 0.0, 12.0),
            "Neck":          (-4.0, 0.0, -28.0),
            "Head":          (-3.5, 0.0, -26.0),
            "RightShoulder": (0.0, -6.0, 16.0),
            "RightUpperArm": (-10.0, 0.0, 30.0),
            "RightLowerArm": (0.0, 46.0, 3.0),
            "RightHand":     (-22.0, 42.0, 0.0),
            # Left hand chambers back cleanly beside cheek
            "LeftShoulder":  (0.0, 14.0, -12.0),
            "LeftUpperArm":  (-8.0, 48.0, -56.0),
            "LeftLowerArm":  (14.0, -38.0, -114.0),
            "LeftUpperLeg":  (-28.0, -5.0, 5.0),
            "LeftLowerLeg":  (24.0, 0.0, 0.0),
            "RightUpperLeg": (18.0, 6.0, -5.0),
            "RightLowerLeg": (10.0, 0.0, 0.0),
        })
        # Crisp front snap kick / teep
        lk_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.025),
            "Hips":          (-6.0, 0.0, 18.0),
            "LeftUpperLeg":  (-78.0, -2.0, 3.0),
            "LeftLowerLeg":  (94.0, 0.0, 0.0),
        })
        lk_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.028),
            "Hips":          (-10.0, 0.0, 16.0),
            "Spine":         (-3.0, 0.0, 4.0),
            "LeftUpperLeg":  (-60.0, -2.0, 3.0),
            "LeftLowerLeg":  (5.0, 0.0, 0.0),
            "LeftFoot":      (26.0, 0.0, 0.0),
            "RightUpperLeg": (8.0, 4.0, -4.0),
            "RightLowerLeg": (14.0, 0.0, 0.0),
        })
        # Technical high roundhouse / crescent kick keeping tight martial guard
        hk_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.028),
            "Hips":          (-10.0, 12.0, -22.0),
            "Spine":         (-6.0, 0.0, 8.0),
            "LeftUpperLeg":  (-98.0, -10.0, 12.0),
            "LeftLowerLeg":  (112.0, 0.0, 0.0),
        })
        hk_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.028),
            "Hips":          (-12.0, 14.0, -28.0),
            "Spine":         (-8.0, 0.0, 10.0),
            "Chest":         (4.0, 0.0, 8.0),
            "Neck":          (6.0, 0.0, 4.0),
            "LeftUpperLeg":  (-110.0, -14.0, 16.0),
            "LeftLowerLeg":  (4.0, 0.0, 0.0),
            "LeftFoot":      (28.0, 0.0, 0.0),
            "RightUpperLeg": (10.0, -6.0, 6.0),
            "RightLowerLeg": (16.0, 0.0, 0.0),
        })

        # Special (Golpe Sombrio — Jompes): low coiled shadow lunge -> explosive centerline spear-thrust
        sp_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.068),
            "Hips":          (3.0, 0.0, -28.0),
            "Spine":         (4.0, 0.0, -20.0),
            "Chest":         (4.0, 0.0, -20.0),
            "Neck":          (-6.0, 0.0, 28.0),
            "Head":          (-5.0, 0.0, 24.0),
            "RightShoulder": (0.0, -14.0, -12.0),
            "RightUpperArm": (10.0, -48.0, 14.0),
            "RightLowerArm": (12.0, 32.0, 118.0),
            "LeftShoulder":  (0.0, 16.0, -16.0),
            "LeftUpperArm":  (-14.0, 36.0, -64.0),
            "LeftLowerArm":  (14.0, -40.0, -112.0),
            "LeftUpperLeg":  (-34.0, -6.0, 6.0),
            "LeftLowerLeg":  (30.0, 0.0, 0.0),
            "RightUpperLeg": (22.0, 6.0, -6.0),
            "RightLowerLeg": (14.0, 0.0, 0.0),
        })
        sp_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.058),
            "Hips":          (3.0, 0.0, 26.0),
            "Spine":         (5.0, 0.0, 16.0),
            "Chest":         (5.0, 0.0, 16.0),
            "UpperChest":    (3.0, 0.0, 10.0),
            "Neck":          (-7.0, 0.0, -28.0),
            "Head":          (-5.0, 0.0, -26.0),
            "RightShoulder": (0.0, -8.0, 18.0),
            "RightUpperArm": (-14.0, 0.0, 16.0),
            "RightLowerArm": (0.0, 48.0, 4.0),
            "RightHand":     (-24.0, 44.0, 0.0),
            "LeftShoulder":  (0.0, 14.0, -14.0),
            "LeftUpperArm":  (-10.0, 50.0, -60.0),
            "LeftLowerArm":  (16.0, -42.0, -118.0),
            "LeftUpperLeg":  (-32.0, -5.0, 6.0),
            "LeftLowerLeg":  (26.0, 0.0, 0.0),
            "RightUpperLeg": (16.0, 6.0, -5.0),
            "RightLowerLeg": (12.0, 0.0, 0.0),
        })
        special_builder = ("standard", sp_ant, sp_hit)

    elif style == "agile_striker":
        # Rochard (Vasco Jacket): fast athletic flick jab, lunge cross, quick low/mid kick & dynamic high kick
        lp_ant = merge_pose(idle0, {
            "Hips":          (0.0, 0.0, 28.0),
            "LeftUpperArm":  (-5.0, 54.0, -42.0),
            "LeftLowerArm":  (10.0, -34.0, -102.0),
        })
        lp_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.026),
            "Hips":          (1.0, 0.0, 6.0),
            "Spine":         (3.5, -4.0, -10.0),
            "Chest":         (3.5, -4.0, -14.0),
            "Neck":          (-4.0, 0.0, 14.0),
            "LeftShoulder":  (0.0, 10.0, -18.0),
            "LeftUpperArm":  (-10.0, 4.0, -60.0),
            "LeftLowerArm":  (0.0, -24.0, -3.0),
            "LeftHand":      (-18.0, -30.0, 0.0),
        })
        hp_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.036),
            "Hips":          (2.0, 0.0, -10.0),
            "Chest":         (3.0, 0.0, -16.0),
            "RightUpperArm": (-4.0, -46.0, 20.0),
            "RightLowerArm": (8.0, 24.0, 104.0),
        })
        hp_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, -0.04, -0.044),
            "Hips":          (3.5, 0.0, 20.0),
            "Spine":         (4.5, 0.0, 14.0),
            "Chest":         (4.5, 0.0, 16.0),
            "Neck":          (-5.0, 0.0, -26.0),
            "RightShoulder": (0.0, -6.0, 16.0),
            "RightUpperArm": (-12.0, 0.0, 32.0),
            "RightLowerArm": (0.0, 44.0, 3.0),
            "RightHand":     (-22.0, 40.0, 0.0),
            "LeftUpperLeg":  (-30.0, -4.0, 4.0),
            "LeftLowerLeg":  (26.0, 0.0, 0.0),
            "RightUpperLeg": (20.0, 5.0, -4.0),
            "RightLowerLeg": (10.0, 0.0, 0.0),
        })
        lk_ant = merge_pose(idle0, {
            "LeftUpperLeg":  (-74.0, -4.0, 4.0),
            "LeftLowerLeg":  (92.0, 0.0, 0.0),
        })
        lk_hit = merge_pose(idle0, {
            "Hips":          (-8.0, 2.0, 16.0),
            "LeftUpperLeg":  (-64.0, -6.0, 6.0),
            "LeftLowerLeg":  (4.0, 0.0, 0.0),
            "LeftFoot":      (24.0, 0.0, 0.0),
        })
        hk_ant = merge_pose(idle0, {
            "Hips":          (-14.0, 6.0, -12.0),
            "RightUpperArm": (-14.0, -48.0, 44.0),
            "LeftUpperLeg":  (-96.0, -8.0, 8.0),
            "LeftLowerLeg":  (108.0, 0.0, 0.0),
        })
        hk_hit = merge_pose(idle0, {
            "Hips":          (-20.0, 10.0, -18.0),
            "Spine":         (-12.0, 0.0, 6.0),
            "Chest":         (4.0, 0.0, 6.0),
            "RightUpperArm": (-18.0, -52.0, 46.0),
            "RightLowerArm": (6.0, 22.0, 84.0),
            "LeftUpperLeg":  (-114.0, -8.0, 8.0),
            "LeftLowerLeg":  (4.0, 0.0, 0.0),
            "LeftFoot":      (28.0, 0.0, 0.0),
        })

        # Special (Tornado Relâmpago — Rochard): quick coil -> high sweeping tornado kick
        sp_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.068),
            "Hips":          (-8.0, 10.0, -34.0),
            "Spine":         (-6.0, 0.0, -14.0),
            "Chest":         (4.0, 0.0, -10.0),
            "RightUpperArm": (-14.0, -44.0, 54.0),
            "LeftUpperArm":  (-14.0, 44.0, -54.0),
            "LeftUpperLeg":  (-96.0, -10.0, 12.0),
            "LeftLowerLeg":  (112.0, 0.0, 0.0),
            "RightUpperLeg": (-24.0, 5.0, -4.0),
            "RightLowerLeg": (34.0, 0.0, 0.0),
        })
        sp_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.036),
            "Hips":          (-22.0, 14.0, -36.0),
            "Spine":         (-14.0, 4.0, 14.0),
            "Chest":         (6.0, 4.0, 14.0),
            "Neck":          (10.0, 0.0, 10.0),
            "Head":          (6.0, 0.0, 8.0),
            "RightUpperArm": (-18.0, -48.0, 52.0),
            "RightLowerArm": (8.0, 26.0, 92.0),
            "LeftUpperArm":  (-16.0, 46.0, -56.0),
            "LeftLowerArm":  (10.0, -28.0, -96.0),
            "LeftUpperLeg":  (-118.0, -12.0, 16.0),
            "LeftLowerLeg":  (4.0, 0.0, 0.0),
            "LeftFoot":      (32.0, 0.0, 0.0),
            "RightUpperLeg": (8.0, -6.0, 6.0),
            "RightLowerLeg": (14.0, 0.0, 0.0),
        })
        special_builder = ("standard", sp_ant, sp_hit)

    else:
        # heavy_brawler (Lucas): strong hip/shoulder torque punches, heavy stomp low kick, heel thrust kick
        lp_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.048),
            "Hips":          (2.0, 0.0, 36.0),
            "Spine":         (4.0, 0.0, 10.0),
            "LeftShoulder":  (0.0, 12.0, -6.0),
            "LeftUpperArm":  (-6.0, 48.0, -40.0),
            "LeftLowerArm":  (12.0, -38.0, -114.0),
            "LeftHand":      (-12.0, -15.0, 10.0),
        })
        lp_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.048),
            "Hips":          (2.0, 0.0, 2.0),
            "Spine":         (4.5, 0.0, -14.0),
            "Chest":         (4.5, 0.0, -18.0),
            "UpperChest":    (3.0, 0.0, -14.0),
            "Neck":          (-5.0, 0.0, 18.0),
            "Head":          (-5.0, 0.0, 18.0),
            "LeftShoulder":  (0.0, 10.0, -22.0),
            "LeftUpperArm":  (-6.0, 0.0, -68.0),
            "LeftLowerArm":  (0.0, -28.0, -4.0),
            "LeftHand":      (-20.0, -35.0, 0.0),
            "RightUpperArm": (-10.0, -48.0, 68.0),
            "RightLowerArm": (14.0, 42.0, 118.0),
        })

        hp_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.062),
            "Hips":          (3.0, 0.0, -18.0),
            "Spine":         (4.0, 0.0, -18.0),
            "Chest":         (3.5, 0.0, -20.0),
            "Neck":          (-5.0, 0.0, 26.0),
            "Head":          (-5.0, 0.0, 24.0),
            "RightShoulder": (0.0, -12.0, -16.0),
            "RightUpperArm": (-6.0, -38.0, 12.0),
            "RightLowerArm": (10.0, 28.0, 112.0),
            "RightHand":     (-16.0, 22.0, -12.0),
        })
        hp_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.066),
            "Hips":          (4.0, 0.0, 22.0),
            "Spine":         (5.0, 0.0, 16.0),
            "Chest":         (5.0, 0.0, 20.0),
            "UpperChest":    (4.0, 0.0, 14.0),
            "Neck":          (-6.0, 0.0, -30.0),
            "Head":          (-6.0, 0.0, -30.0),
            "RightShoulder": (0.0, -8.0, 18.0),
            "RightUpperArm": (-12.0, 0.0, 28.0),
            "RightLowerArm": (0.0, 46.0, 5.0),
            "RightHand":     (-24.0, 44.0, 0.0),
            "LeftShoulder":  (0.0, 16.0, -12.0),
            "LeftUpperArm":  (-12.0, 50.0, -56.0),
            "LeftLowerArm":  (14.0, -38.0, -112.0),
            "LeftUpperLeg":  (-32.0, -5.0, 6.0),
            "LeftLowerLeg":  (28.0, 0.0, 0.0),
            "RightUpperLeg": (24.0, 7.0, -6.0),
            "RightLowerLeg": (12.0, 0.0, 0.0),
        })

        # Heavy low stomp kick at shin/knee height
        lk_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.045),
            "Hips":          (-6.0, 0.0, 16.0),
            "LeftUpperLeg":  (-62.0, -5.0, 5.0),
            "LeftLowerLeg":  (74.0, 0.0, 0.0),
            "RightUpperLeg": (10.0, 5.0, -5.0),
            "RightLowerLeg": (18.0, 0.0, 0.0),
        })
        lk_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.052),
            "Hips":          (-8.0, 0.0, 14.0),
            "Spine":         (-4.0, 0.0, 6.0),
            "LeftUpperLeg":  (-46.0, -4.0, 5.0),
            "LeftLowerLeg":  (10.0, 0.0, 0.0),
            "LeftFoot":      (-10.0, 0.0, 0.0),
            "RightUpperLeg": (10.0, 5.0, -5.0),
            "RightLowerLeg": (18.0, 0.0, 0.0),
        })

        # Powerful side/front heel thrust kick with solid counterweight lean
        hk_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.050),
            "Hips":          (-14.0, 8.0, -18.0),
            "Spine":         (-8.0, 0.0, 8.0),
            "Chest":         (4.0, 0.0, 8.0),
            "Neck":          (10.0, 0.0, 4.0),
            "LeftUpperLeg":  (-88.0, -10.0, 10.0),
            "LeftLowerLeg":  (106.0, 0.0, 0.0),
            "RightUpperLeg": (10.0, -6.0, 6.0),
            "RightLowerLeg": (18.0, 0.0, 0.0),
        })
        hk_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.048),
            "Hips":          (-18.0, 10.0, -24.0),
            "Spine":         (-12.0, 0.0, 8.0),
            "Chest":         (6.0, 0.0, 12.0),
            "Neck":          (12.0, 0.0, 6.0),
            "Head":          (6.0, 0.0, 2.0),
            "LeftUpperLeg":  (-90.0, -10.0, 12.0),
            "LeftLowerLeg":  (4.0, 0.0, 0.0),
            "LeftFoot":      (-18.0, 0.0, 0.0),
            "RightUpperLeg": (10.0, -6.0, 8.0),
            "RightLowerLeg": (18.0, 0.0, 0.0),
            "RightFoot":     (-12.0, 0.0, 0.0),
        })

        # Special (Martelo do Asfalto — Lucas): twin-fist crouch coil -> devastating overhead hammer smash
        sp_ant = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.095),
            "Hips":          (-4.0, 0.0, 14.0),
            "Spine":         (-10.0, 0.0, 4.0),
            "Chest":         (-10.0, 0.0, 4.0),
            "UpperChest":    (-6.0, 0.0, 2.0),
            "Neck":          (8.0, 0.0, -10.0),
            "Head":          (6.0, 0.0, -8.0),
            "LeftShoulder":  (0.0, 18.0, -18.0),
            "LeftUpperArm":  (-26.0, 32.0, -78.0),
            "LeftLowerArm":  (18.0, -40.0, -102.0),
            "LeftHand":      (-12.0, 18.0, 14.0),
            "RightShoulder": (0.0, -18.0, 18.0),
            "RightUpperArm": (-26.0, -32.0, 78.0),
            "RightLowerArm": (18.0, 40.0, 102.0),
            "RightHand":     (-12.0, -18.0, -14.0),
            "LeftUpperLeg":  (-34.0, -5.0, 6.0),
            "LeftLowerLeg":  (46.0, 0.0, 0.0),
            "RightUpperLeg": (14.0, 6.0, -6.0),
            "RightLowerLeg": (28.0, 0.0, 0.0),
        })
        sp_hit = merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.072),
            "Hips":          (5.0, 0.0, 10.0),
            "Spine":         (7.0, 0.0, 6.0),
            "Chest":         (7.0, 0.0, 6.0),
            "UpperChest":    (5.0, 0.0, 4.0),
            "Neck":          (-9.0, 0.0, -12.0),
            "Head":          (-8.0, 0.0, -10.0),
            "LeftShoulder":  (0.0, 10.0, -22.0),
            "LeftUpperArm":  (-22.0, 6.0, -46.0),
            "LeftLowerArm":  (10.0, -36.0, -48.0),
            "LeftHand":      (-18.0, -26.0, 0.0),
            "RightShoulder": (0.0, -10.0, 22.0),
            "RightUpperArm": (-22.0, -6.0, 46.0),
            "RightLowerArm": (10.0, 36.0, 48.0),
            "RightHand":     (-18.0, 26.0, 0.0),
            "LeftUpperLeg":  (-36.0, -6.0, 7.0),
            "LeftLowerLeg":  (34.0, 0.0, 0.0),
            "RightUpperLeg": (24.0, 7.0, -6.0),
            "RightLowerLeg": (14.0, 0.0, 0.0),
        })
        special_builder = ("standard", sp_ant, sp_hit)

    move_poses = {
        "LightPunch": (lp_ant, lp_hit),
        "HeavyPunch": (hp_ant, hp_hit),
        "LightKick":  (lk_ant, lk_hit),
        "HeavyKick":  (hk_ant, hk_hit),
    }

    for move_name, (ant_pose, hit_pose) in move_poses.items():
        startup, active, recovery = framedata[move_name]
        ant_f, hit_start, hit_end, end_f = _attack_key_frames(startup, active, recovery)
        anims[move_name] = {
            1: idle0,
            ant_f: ant_pose,
            hit_start: hit_pose,
            hit_end: hit_pose,
            end_f: idle0,
        }

    sp_startup, sp_active, sp_recovery = framedata["Special"]
    sp_ant_f, sp_hit_start, sp_hit_end, sp_end_f = _attack_key_frames(sp_startup, sp_active, sp_recovery)
    if special_builder[0] == "multi":
        _, sp_ant, sp_hit1, sp_hit2 = special_builder
        sp_mid_f = sp_hit_start + max(2, sp_active // 2)
        anims["Special"] = {
            1: idle0,
            sp_ant_f: sp_ant,
            sp_hit_start: sp_hit1,
            sp_mid_f: sp_hit2,
            sp_hit_end: sp_hit2,
            sp_end_f: idle0,
        }
    else:
        _, sp_ant, sp_hit = special_builder
        anims["Special"] = {
            1: idle0,
            sp_ant_f: sp_ant,
            sp_hit_start: sp_hit,
            sp_hit_end: sp_hit,
            sp_end_f: idle0,
        }

    # CrouchKick (low sweeping kick from crouch posture)
    crouch_base = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.35),
        "Hips":          (14.0, 0.0, 20.0),
        "Spine":         (10.0, 0.0, 4.0),
        "Chest":         (8.0, 0.0, 2.0),
        "Neck":          (-14.0, 0.0, -14.0),
        "Head":          (-12.0, 0.0, -12.0),
        "LeftUpperLeg":  (-92.0, -8.0, 10.0),
        "LeftLowerLeg":  (98.0, 0.0, 0.0),
        "LeftFoot":      (-6.0, 0.0, 0.0),
        "RightUpperLeg": (-88.0, 8.0, -10.0),
        "RightLowerLeg": (96.0, 0.0, 0.0),
        "RightFoot":     (-8.0, 0.0, 0.0),
    })
    ck_sweep = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.44),
        "Hips":          (-10.0, 8.0, -22.0),
        "Spine":         (-6.0, 0.0, 8.0),
        "Chest":         (4.0, 0.0, 8.0),
        "Neck":          (6.0, 0.0, 12.0),
        "Head":          (4.0, 0.0, 10.0),
        "LeftUpperArm":  (-10.0, 46.0, -54.0),
        "LeftLowerArm":  (12.0, -34.0, -104.0),
        "RightUpperArm": (-14.0, -46.0, 54.0),
        "RightLowerArm": (12.0, 34.0, 104.0),
        "LeftUpperLeg":  (-76.0, -12.0, 16.0),
        "LeftLowerLeg":  (6.0, 0.0, 0.0),
        "LeftFoot":      (22.0, 0.0, 0.0),
        "RightUpperLeg": (-92.0, 14.0, -16.0),
        "RightLowerLeg": (104.0, 0.0, 0.0),
        "RightFoot":     (-10.0, 0.0, 0.0),
    })
    ck_ant_f, ck_hit_start, ck_hit_end, ck_end_f = _attack_key_frames(*framedata["CrouchKick"])
    anims["CrouchKick"] = {
        1: crouch_base,
        ck_ant_f: crouch_base,
        ck_hit_start: ck_sweep,
        ck_hit_end: ck_sweep,
        ck_end_f: idle0,
    }

    # JumpAttack (aerial downward-forward flying kick, 18 frames: 1..19)
    ja_tuck = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, 0.04),
        "Spine":         (8.0, 0.0, 4.0),
        "LeftUpperArm":  (-14.0, 42.0, -56.0),
        "RightUpperArm": (-14.0, -42.0, 56.0),
        "LeftUpperLeg":  (-78.0, -4.0, 6.0),
        "LeftLowerLeg":  (95.0, 0.0, 0.0),
        "LeftFoot":      (20.0, 0.0, 0.0),
        "RightUpperLeg": (-58.0, 4.0, -6.0),
        "RightLowerLeg": (88.0, 0.0, 0.0),
        "RightFoot":     (20.0, 0.0, 0.0),
    })
    ja_hit = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, 0.08),
        "Hips":          (-18.0, 6.0, -16.0),
        "Spine":         (-10.0, 0.0, 8.0),
        "Chest":         (4.0, 0.0, 6.0),
        "Neck":          (8.0, 0.0, 8.0),
        "Head":          (4.0, 0.0, 6.0),
        "LeftUpperArm":  (-14.0, 44.0, -62.0),
        "LeftLowerArm":  (14.0, -36.0, -106.0),
        "RightUpperArm": (-14.0, -44.0, 62.0),
        "RightLowerArm": (14.0, 36.0, 106.0),
        "LeftUpperLeg":  (-68.0, -6.0, 8.0),
        "LeftLowerLeg":  (8.0, 0.0, 0.0),
        "LeftFoot":      (22.0, 0.0, 0.0),
        "RightUpperLeg": (-76.0, 8.0, -10.0),
        "RightLowerLeg": (102.0, 0.0, 0.0),
        "RightFoot":     (18.0, 0.0, 0.0),
    })
    ja_ant_f, ja_hit_start, ja_hit_end, ja_end_f = _attack_key_frames(*framedata["JumpAttack"])
    anims["JumpAttack"] = {
        1: ja_tuck,
        ja_ant_f: ja_tuck,
        ja_hit_start: ja_hit,
        ja_hit_end: ja_hit,
        ja_end_f: ja_tuck,
    }

    # Throw (close-range two-handed grab -> firm shoulder/torso throw, 22 frames: 1..23)
    grip_hands = relaxed_grip(0.45)
    th_grab = merge_pose(idle0, merge_pose(grip_hands, {
        "HipsOffset":    (0.0, 0.0, -0.055),
        "Hips":          (4.0, 0.0, 14.0),
        "Spine":         (6.0, 0.0, 4.0),
        "Chest":         (6.0, 0.0, 2.0),
        "Neck":          (-8.0, 0.0, -12.0),
        "Head":          (-6.0, 0.0, -10.0),
        "LeftShoulder":  (0.0, 12.0, -20.0),
        "LeftUpperArm":  (-14.0, 8.0, -56.0),
        "LeftLowerArm":  (10.0, -24.0, -22.0),
        "LeftHand":      (-14.0, -18.0, 8.0),
        "RightShoulder": (0.0, -12.0, 20.0),
        "RightUpperArm": (-14.0, -8.0, 56.0),
        "RightLowerArm": (10.0, 24.0, 22.0),
        "RightHand":     (-14.0, 18.0, -8.0),
        "LeftUpperLeg":  (-32.0, -5.0, 6.0),
        "LeftLowerLeg":  (22.0, 0.0, 0.0),
        "RightUpperLeg": (18.0, 6.0, -6.0),
        "RightLowerLeg": (12.0, 0.0, 0.0),
    }))
    th_throw = merge_pose(idle0, merge_pose(grip_hands, {
        "HipsOffset":    (0.0, 0.0, -0.068),
        "Hips":          (6.0, 0.0, -18.0),
        "Spine":         (8.0, -4.0, -18.0),
        "Chest":         (8.0, -4.0, -16.0),
        "UpperChest":    (4.0, -2.0, -10.0),
        "Neck":          (-8.0, 0.0, 24.0),
        "Head":          (-6.0, 0.0, 22.0),
        "LeftShoulder":  (0.0, 14.0, -14.0),
        "LeftUpperArm":  (-18.0, 26.0, -72.0),
        "LeftLowerArm":  (14.0, -34.0, -78.0),
        "RightShoulder": (0.0, -14.0, 18.0),
        "RightUpperArm": (-24.0, -12.0, 38.0),
        "RightLowerArm": (10.0, 28.0, 44.0),
        "LeftUpperLeg":  (-34.0, -5.0, 6.0),
        "LeftLowerLeg":  (26.0, 0.0, 0.0),
        "RightUpperLeg": (20.0, 6.0, -6.0),
        "RightLowerLeg": (14.0, 0.0, 0.0),
    }))
    th_ant_f, th_hit_start, th_hit_end, th_end_f = _attack_key_frames(*framedata["Throw"])
    anims["Throw"] = {
        1: idle0,
        th_ant_f: th_grab,
        th_hit_start: th_throw,
        th_hit_end: th_throw,
        th_end_f: idle0,
    }

    return anims


def build_common_actions(idle0):
    anims = {}

    # Jump (36 frames)
    jump_prep = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.18),
        "Spine":         (12.0, 0.0, 4.0),
        "LeftUpperLeg":  (-55.0, -4.0, 6.0),
        "LeftLowerLeg":  (68.0, 0.0, 0.0),
        "LeftFoot":      (-14.0, 0.0, 0.0),
        "RightUpperLeg": (-45.0, 4.0, -6.0),
        "RightLowerLeg": (62.0, 0.0, 0.0),
        "RightFoot":     (-18.0, 0.0, 0.0),
    })
    jump_peak = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, 0.42),
        "Spine":         (8.0, 0.0, 4.0),
        "LeftUpperArm":  (-15.0, 40.0, -52.0),
        "RightUpperArm": (-15.0, -40.0, 52.0),
        "LeftUpperLeg":  (-78.0, -4.0, 6.0),
        "LeftLowerLeg":  (95.0, 0.0, 0.0),
        "LeftFoot":      (25.0, 0.0, 0.0),
        "RightUpperLeg": (-52.0, 4.0, -6.0),
        "RightLowerLeg": (85.0, 0.0, 0.0),
        "RightFoot":     (25.0, 0.0, 0.0),
    })
    anims["Jump"] = {1: idle0, 6: jump_prep, 18: jump_peak, 30: jump_prep, 37: idle0}

    # Crouch (30 frames: hold crouch pose through frame 31)
    crouch_pose = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.35),
        "Hips":          (14.0, 0.0, 20.0),
        "Spine":         (10.0, 0.0, 4.0),
        "Chest":         (8.0, 0.0, 2.0),
        "Neck":          (-14.0, 0.0, -14.0),
        "Head":          (-12.0, 0.0, -12.0),
        "LeftUpperLeg":  (-92.0, -8.0, 10.0),
        "LeftLowerLeg":  (98.0, 0.0, 0.0),
        "LeftFoot":      (-6.0, 0.0, 0.0),
        "RightUpperLeg": (-88.0, 8.0, -10.0),
        "RightLowerLeg": (96.0, 0.0, 0.0),
        "RightFoot":     (-8.0, 0.0, 0.0),
    })
    anims["Crouch"] = {1: idle0, 6: crouch_pose, 31: crouch_pose}

    # HitReact (20 frames)
    hit_peak = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.06),
        "Hips":          (-10.0, 0.0, 18.0),
        "Spine":         (-14.0, 0.0, 0.0),
        "Chest":         (-16.0, 0.0, 0.0),
        "Neck":          (-18.0, 0.0, -10.0),
        "Head":          (-18.0, 0.0, -10.0),
        "LeftUpperArm":  (-12.0, 42.0, -35.0),
        "LeftLowerArm":  (10.0, -20.0, -70.0),
        "RightUpperArm": (-12.0, -42.0, 35.0),
        "RightLowerArm": (10.0, 20.0, 70.0),
    })
    anims["HitReact"] = {1: idle0, 5: hit_peak, 9: hit_peak, 21: idle0}

    # KnockDown (45 frames)
    kd_stagger = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.09),
        "Hips":          (-18.0, 0.0, 15.0),
        "Spine":         (-16.0, 0.0, 0.0),
        "Chest":         (-16.0, 0.0, 0.0),
        "Neck":          (-15.0, 0.0, -10.0),
        "LeftUpperArm":  (-15.0, 30.0, -25.0),
        "RightUpperArm": (-15.0, -30.0, 25.0),
    })
    kd_midfall = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.42),
        "Hips":          (-55.0, 0.0, 0.0),
        "Spine":         (-10.0, 0.0, 0.0),
        "Chest":         (-8.0, 0.0, 0.0),
        "Neck":          (15.0, 0.0, 0.0),
        "LeftUpperArm":  (-10.0, 25.0, -25.0),
        "LeftLowerArm":  (0.0, -10.0, -35.0),
        "RightUpperArm": (-10.0, -25.0, 25.0),
        "RightLowerArm": (0.0, 10.0, 35.0),
        "LeftUpperLeg":  (-25.0, -8.0, 8.0),
        "LeftLowerLeg":  (35.0, 0.0, 0.0),
        "RightUpperLeg": (-20.0, 8.0, -8.0),
        "RightLowerLeg": (30.0, 0.0, 0.0),
    })
    kd_ground = {
        "HipsOffset":    (0.0, 0.0, -0.76),
        "Hips":          (-88.0, 0.0, 0.0),
        "Spine":         (2.0, 0.0, 0.0),
        "Chest":         (2.0, 0.0, 0.0),
        "UpperChest":    (2.0, 0.0, 0.0),
        "Neck":          (5.0, 0.0, 0.0),
        "Head":          (6.0, 0.0, 0.0),
        "LeftShoulder":  (0.0, 8.0, 0.0),
        "LeftUpperArm":  (0.0, 8.0, -28.0),
        "LeftLowerArm":  (0.0, 0.0, -18.0),
        "RightShoulder": (0.0, -8.0, 0.0),
        "RightUpperArm": (0.0, -8.0, 28.0),
        "RightLowerArm": (0.0, 0.0, 18.0),
        "LeftUpperLeg":  (-10.0, -6.0, 8.0),
        "LeftLowerLeg":  (18.0, 0.0, 0.0),
        "LeftFoot":      (10.0, 0.0, 0.0),
        "RightUpperLeg": (-8.0, 6.0, -8.0),
        "RightLowerLeg": (14.0, 0.0, 0.0),
        "RightFoot":     (10.0, 0.0, 0.0),
    }
    anims["KnockDown"] = {1: idle0, 10: kd_stagger, 24: kd_midfall, 36: kd_ground, 46: kd_ground}

    # GetUp (40 frames)
    gu_push = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.45),
        "Hips":          (22.0, 0.0, 15.0),
        "Spine":         (18.0, 0.0, 0.0),
        "Chest":         (12.0, 0.0, 0.0),
        "Neck":          (-15.0, 0.0, -10.0),
        "LeftUpperLeg":  (-85.0, -5.0, 8.0),
        "LeftLowerLeg":  (95.0, 0.0, 0.0),
        "RightUpperLeg": (-45.0, 5.0, -8.0),
        "RightLowerLeg": (90.0, 0.0, 0.0),
    })
    anims["GetUp"] = {1: kd_ground, 18: gu_push, 32: crouch_pose, 41: idle0}

    # Defeat (50 frames)
    def_knees = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.46),
        "Hips":          (18.0, 0.0, 0.0),
        "Spine":         (24.0, 0.0, 0.0),
        "Chest":         (22.0, 0.0, 0.0),
        "Neck":          (20.0, 0.0, 0.0),
        "Head":          (20.0, 0.0, 0.0),
        "LeftUpperArm":  (0.0, 65.0, -20.0),
        "LeftLowerArm":  (0.0, 0.0, -35.0),
        "RightUpperArm": (0.0, -65.0, 20.0),
        "RightLowerArm": (0.0, 0.0, 35.0),
        "LeftUpperLeg":  (-55.0, -5.0, 6.0),
        "LeftLowerLeg":  (110.0, 0.0, 0.0),
        "RightUpperLeg": (-55.0, 5.0, -6.0),
        "RightLowerLeg": (110.0, 0.0, 0.0),
    })
    anims["Defeat"] = {1: idle0, 18: def_knees, 36: def_knees, 51: kd_ground}

    # Turn180 (8 frames: quick planted foot-pivot turn for 180-deg cross-up side swap)
    base_hz = idle0.get("HipsOffset", (0.0, 0.0, 0.0))[2]
    hips_rz = idle0.get("Hips", (0.0, 0.0, 22.0))[2]
    turn_start = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, base_hz - 0.018),
        "Hips":          (1.5, 0.0, hips_rz - 14.0),
        "Spine":         (2.5, 0.0, 0.0),
        "Chest":         (2.0, 0.0, -4.0),
        "Neck":          (-2.5, 0.0, -14.0),
        "Head":          (-2.0, 0.0, -16.0),
        "LeftUpperLeg":  (-31.0, -5.0, 5.0),
        "LeftLowerLeg":  (16.0, 0.0, 0.0),
        "LeftFoot":      (15.0, 0.0, 0.0),
        "RightUpperLeg": (-2.0, 5.0, -5.0),
        "RightLowerLeg": (24.0, 0.0, 0.0),
        "RightFoot":     (-18.0, 0.0, 0.0),
    })
    turn_mid = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, base_hz - 0.025),
        "Hips":          (2.0, 0.0, hips_rz - 22.0),
        "Spine":         (3.5, 0.0, -6.0),
        "Chest":         (2.5, 0.0, -6.0),
        "Neck":          (-3.0, 0.0, -22.0),
        "Head":          (-2.5, 0.0, -22.0),
        "LeftUpperLeg":  (-32.0, -4.0, 4.0),
        "LeftLowerLeg":  (18.0, 0.0, 0.0),
        "LeftFoot":      (14.0, 0.0, 0.0),
        "RightUpperLeg": (-24.0, 4.0, -4.0),
        "RightLowerLeg": (44.0, 0.0, 0.0),
        "RightFoot":     (-12.0, 0.0, 0.0),
    })
    turn_end = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, base_hz - 0.012),
        "Hips":          (1.5, 0.0, hips_rz - 8.0),
        "Spine":         (2.5, 0.0, 0.0),
        "Chest":         (2.0, 0.0, 0.0),
        "Neck":          (-2.5, 0.0, -12.0),
        "Head":          (-2.0, 0.0, -12.0),
        "LeftUpperLeg":  (-30.0, -5.0, 5.0),
        "LeftLowerLeg":  (13.0, 0.0, 0.0),
        "LeftFoot":      (17.0, 0.0, 0.0),
        "RightUpperLeg": (0.0, 5.0, -5.0),
        "RightLowerLeg": (22.0, 0.0, 0.0),
        "RightFoot":     (-20.0, 0.0, 0.0),
    })
    anims["Turn180"] = {1: idle0, 3: turn_start, 5: turn_mid, 7: turn_end, 9: idle0}

    # DashForward (14 frames: 1..15, explosive forward drive with tight guard returning to Idle)
    dash_fwd_drive = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.075),
        "Hips":          (8.0, 0.0, 18.0),
        "Spine":         (10.0, 0.0, 4.0),
        "Chest":         (8.0, 0.0, 2.0),
        "UpperChest":    (4.0, 0.0, 2.0),
        "Neck":          (-12.0, 0.0, -14.0),
        "Head":          (-8.0, 0.0, -14.0),
        "LeftUpperLeg":  (-42.0, -5.0, 6.0),
        "LeftLowerLeg":  (26.0, 0.0, 0.0),
        "LeftFoot":      (10.0, 0.0, 0.0),
        "RightUpperLeg": (28.0, 6.0, -6.0),
        "RightLowerLeg": (16.0, 0.0, 0.0),
        "RightFoot":     (-18.0, 0.0, 0.0),
    })
    dash_fwd_glide = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.055),
        "Hips":          (6.0, 0.0, 20.0),
        "Spine":         (7.0, 0.0, 4.0),
        "Chest":         (5.0, 0.0, 2.0),
        "Neck":          (-8.0, 0.0, -14.0),
        "Head":          (-6.0, 0.0, -14.0),
        "LeftUpperLeg":  (-34.0, -5.0, 6.0),
        "LeftLowerLeg":  (16.0, 0.0, 0.0),
        "LeftFoot":      (14.0, 0.0, 0.0),
        "RightUpperLeg": (18.0, 6.0, -6.0),
        "RightLowerLeg": (22.0, 0.0, 0.0),
        "RightFoot":     (-20.0, 0.0, 0.0),
    })
    anims["DashForward"] = {1: idle0, 5: dash_fwd_drive, 10: dash_fwd_glide, 15: idle0}

    # DashBackward (16 frames: 1..17, elastic backdash evading backward then cushioning into Idle)
    dash_back_evade = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.035),
        "Hips":          (-8.0, 0.0, 20.0),
        "Spine":         (-10.0, 0.0, 4.0),
        "Chest":         (-8.0, 0.0, 2.0),
        "Neck":          (8.0, 0.0, -14.0),
        "Head":          (6.0, 0.0, -14.0),
        "LeftUpperLeg":  (-20.0, -4.0, 5.0),
        "LeftLowerLeg":  (36.0, 0.0, 0.0),
        "LeftFoot":      (-8.0, 0.0, 0.0),
        "RightUpperLeg": (14.0, 5.0, -5.0),
        "RightLowerLeg": (22.0, 0.0, 0.0),
        "RightFoot":     (-18.0, 0.0, 0.0),
    })
    dash_back_land = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.065),
        "Hips":          (2.0, 0.0, 22.0),
        "Spine":         (4.0, 0.0, 4.0),
        "LeftUpperLeg":  (-32.0, -5.0, 6.0),
        "LeftLowerLeg":  (24.0, 0.0, 0.0),
        "LeftFoot":      (8.0, 0.0, 0.0),
        "RightUpperLeg": (20.0, 6.0, -6.0),
        "RightLowerLeg": (18.0, 0.0, 0.0),
        "RightFoot":     (-22.0, 0.0, 0.0),
    })
    anims["DashBackward"] = {1: idle0, 5: dash_back_evade, 11: dash_back_land, 17: idle0}

    return anims


def _get_clip_ground_modes(clip_name, style, keyframes):
    frames = sorted(keyframes.keys())
    first_f, last_f = frames[0], frames[-1]
    if clip_name in ("Idle", "BlockStand", "Crouch", "Victory", "HitReact", "LightPunch", "HeavyPunch", "Throw"):
        return {f: "both" for f in frames}
    if clip_name == "Special":
        if style == "agile_striker":
            return {f: ("both" if f in (first_f, last_f) else "right") for f in frames}
        return {f: "both" for f in frames}
    if clip_name in ("LightKick", "HeavyKick"):
        return {f: ("both" if f in (first_f, last_f) else "right") for f in frames}
    if clip_name == "CrouchKick":
        return {
            frames[0]: "both",
            frames[1]: "both",
            frames[2]: "min",
            frames[3]: "min",
            frames[4]: "both",
        }
    if clip_name == "JumpAttack":
        return {}
    if clip_name == "DashForward":
        return {1: "both", 5: "min", 10: "min", 15: "both"}
    if clip_name == "DashBackward":
        return {1: "both", 5: "min", 11: "min", 17: "both"}
    if clip_name in ("WalkForward", "WalkBackward"):
        return {f: "min" for f in frames}
    if clip_name == "Jump":
        return {1: "both", 6: "both", 30: "both", 37: "both"}
    if clip_name == "KnockDown":
        return {1: "both", 10: "both", 24: "min", 36: "min", 46: "min"}
    if clip_name == "GetUp":
        return {1: "min", 18: "min", 32: "both", 41: "both"}
    if clip_name == "Defeat":
        return {1: "both", 18: "min", 36: "min", 51: "min"}
    if clip_name == "Turn180":
        return {1: "both", 3: "both", 5: "min", 7: "both", 9: "both"}
    return {}


def verify_and_report_grounding(arm_obj, mesh_obj, foot_vdata, name, framedata):
    """Verify minimum world Z across Idle (all 61 frames) and key grounded action frames."""
    if mesh_obj is None:
        return {}
    scene = bpy.context.scene
    arm_obj.data.pose_position = 'POSE'
    report = {}

    # 1) All 61 frames of Idle
    arm_obj.animation_data.action = bpy.data.actions["Idle"]
    idle_zs = []
    idle_lr_diffs = []
    for f in range(1, 62):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        lz = _eval_mesh_min_z(mesh_obj, foot_vdata["Left"]["all"])
        rz = _eval_mesh_min_z(mesh_obj, foot_vdata["Right"]["all"])
        idle_zs.append(min(lz, rz))
        idle_lr_diffs.append(abs(lz - rz))
    report["Idle"] = (min(idle_zs), max(idle_zs), max(idle_lr_diffs))

    # 2) Keyframes of other grounded clips + Special impact frame
    sp_hit_f = 1 + framedata["Special"][0] + 1
    hp_hit_f = 1 + framedata["HeavyPunch"][0] + 1
    hk_hit_f = 1 + framedata["HeavyKick"][0] + 1
    ck_hit_f = 1 + framedata["CrouchKick"][0] + 1
    th_hit_f = 1 + framedata["Throw"][0] + 1
    checks = [
        ("BlockStand", 20),
        ("Crouch", 20),
        ("DashForward", 5),
        ("DashBackward", 5),
        ("Victory", 50),
        ("Turn180", 5),
        ("HeavyPunch", hp_hit_f),
        ("HeavyKick", hk_hit_f),
        ("CrouchKick", ck_hit_f),
        ("Throw", th_hit_f),
        ("Special", sp_hit_f),
    ]
    for cname, f in checks:
        arm_obj.animation_data.action = bpy.data.actions[cname]
        scene.frame_set(f)
        bpy.context.view_layer.update()
        mz = _eval_mesh_min_z(mesh_obj, None)
        report[f"{cname}@{f}"] = mz

    arm_obj.animation_data.action = None
    reset_pose(arm_obj)
    bpy.context.view_layer.update()
    print(
        f"[Grounding Check] {name}: Idle z=[{report['Idle'][0]:+.4f}, {report['Idle'][1]:+.4f}] m "
        f"(L/R diff<={report['Idle'][2]:.4f} m), "
        + ", ".join(f"{k}={v:+.4f}m" for k, v in report.items() if k != "Idle")
    )
    return report


def generate_all_animations(arm_obj, config=None):
    config = config or {}
    set_scene_fps(config.get("fps", 60))
    arm_obj.data.pose_position = 'POSE'

    weight_class, style, framedata = get_fighter_framedata(config)
    mesh_name = config.get("mesh_name")
    mesh_obj = bpy.data.objects.get(mesh_name) if mesh_name else None
    foot_vdata = _collect_foot_vertex_groups(mesh_obj)

    idle0 = get_idle_stance(0.0, style=style)
    idle_up = get_idle_stance(1.0, style=style)
    idle_dn = get_idle_stance(-1.0, style=style)

    animations = {}
    animations["Idle"] = {1: idle0, 16: idle_up, 31: idle0, 46: idle_dn, 61: idle0}
    animations.update(build_style_locomotion(style, idle0))
    animations.update(build_common_actions(idle0))
    animations.update(build_style_block_and_victory(style, idle0))
    animations.update(build_style_attacks(style, idle0, framedata))

    # Individual per-character animation overrides hook
    anim_overrides = config.get("anim_overrides") or {}
    for clip_name, override_val in anim_overrides.items():
        if callable(override_val):
            animations[clip_name] = override_val(idle0, framedata)
        else:
            animations[clip_name] = override_val

    # Apply closed combat fists (or relaxed grip on hit/fall states) across all keyframes
    loose_clips = {"HitReact", "KnockDown", "GetUp", "Defeat"}
    for clip_name, kf in animations.items():
        if clip_name in loose_clips:
            hand_pose = relaxed_grip(0.45)
        elif style == "street_scrappy" and clip_name == "Idle":
            hand_pose = closed_fist(0.97)
        else:
            hand_pose = closed_fist(1.0)
        for frame_idx, pose_dict in kf.items():
            kf[frame_idx] = merge_pose(hand_pose, pose_dict)

    ordered_names = [
        "Idle", "WalkForward", "WalkBackward", "DashForward", "DashBackward",
        "Jump", "Crouch", "BlockStand",
        "LightPunch", "HeavyPunch", "LightKick", "HeavyKick",
        "CrouchKick", "JumpAttack", "Throw", "Special",
        "HitReact", "KnockDown", "GetUp", "Victory", "Defeat", "Turn180",
    ]
    for name in ordered_names:
        kf = animations[name]
        ground_modes = _get_clip_ground_modes(name, style, kf)
        build_action_from_keyframes(
            arm_obj,
            name,
            kf,
            mesh_obj=mesh_obj,
            foot_vdata=foot_vdata,
            frame_ground_modes=ground_modes,
        )
        print(f"Generated [{style}/{weight_class}] action '{name}' (frames {min(kf.keys())}..{max(kf.keys())}, len={max(kf.keys()) - min(kf.keys())})")

    verify_and_report_grounding(arm_obj, mesh_obj, foot_vdata, config.get("name", "Fighter"), framedata)

    arm_obj.animation_data.action = bpy.data.actions["Idle"]
    bpy.context.scene.frame_set(1)
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 61
    return ordered_names


# ---------------------------------------------------------------------------
# Export FBX for Unity (Y Up, +Z Forward in Unity, Scale 1.0, 60 FPS)
# ---------------------------------------------------------------------------

def export_character_fbx(config):
    arm_obj = bpy.data.objects[config["armature_name"]]
    mesh_obj = bpy.data.objects[config["mesh_name"]]
    out_fbx = config["export_fbx"]
    os.makedirs(os.path.dirname(out_fbx), exist_ok=True)

    bpy.ops.object.mode_set(mode='OBJECT')

    # CRITICAL: Reset to pure Rest T-Pose before exporting FBX while keeping
    # pose_position='POSE' so Unity Humanoid Avatar reads pure T-Pose as Rest Pose
    # and all 16 actions still bake via bake_anim_use_all_actions=True.
    arm_obj.data.pose_position = 'POSE'
    if arm_obj.animation_data:
        arm_obj.animation_data.action = None
    reset_pose(arm_obj)
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()

    bpy.ops.object.select_all(action='DESELECT')
    arm_obj.select_set(True)
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj

    bpy.ops.export_scene.fbx(
        filepath=out_fbx,
        use_selection=True,
        object_types={'ARMATURE', 'MESH'},
        global_scale=1.0,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        use_space_transform=True,
        bake_space_transform=False,
        mesh_smooth_type='FACE',
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        primary_bone_axis='Y',
        secondary_bone_axis='X',
        armature_nodetype='NULL',
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode='COPY',
        embed_textures=True,
    )
    print(f"Exported FBX to: {out_fbx}")


# ---------------------------------------------------------------------------
# Mesh Cleaning, Selective Decimation, 52-Bone Rig & Weight Refinement
# ---------------------------------------------------------------------------

BONE_PARENTS = {
    "Hips": (None, False),
    "Spine": ("Hips", True),
    "Chest": ("Spine", True),
    "UpperChest": ("Chest", True),
    "Neck": ("UpperChest", True),
    "Head": ("Neck", True),
    "LeftShoulder": ("UpperChest", False),
    "LeftUpperArm": ("LeftShoulder", True),
    "LeftLowerArm": ("LeftUpperArm", True),
    "LeftHand": ("LeftLowerArm", True),
    "RightShoulder": ("UpperChest", False),
    "RightUpperArm": ("RightShoulder", True),
    "RightLowerArm": ("RightUpperArm", True),
    "RightHand": ("RightLowerArm", True),
    "LeftUpperLeg": ("Hips", False),
    "LeftLowerLeg": ("LeftUpperLeg", True),
    "LeftFoot": ("LeftLowerLeg", True),
    "LeftToes": ("LeftFoot", True),
    "RightUpperLeg": ("Hips", False),
    "RightLowerLeg": ("RightUpperLeg", True),
    "RightFoot": ("RightLowerLeg", True),
    "RightToes": ("RightFoot", True),
}

FINGER_NAMES = ["Thumb", "Index", "Middle", "Ring", "Little"]
FINGER_SEGMENTS = ["Proximal", "Intermediate", "Distal"]


def setup_character_material(mesh_obj, config):
    """Create or rebuild PBR material with basecolor, normal, and metallic/smoothness maps."""
    mat_name = config["material_name"]
    if mat_name in bpy.data.materials:
        bpy.data.materials.remove(bpy.data.materials[mat_name])

    mat = bpy.data.materials.new(name=mat_name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()

    out_node = nt.nodes.new("ShaderNodeOutputMaterial")
    out_node.location = (400, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (100, 0)
    nt.links.new(bsdf.outputs["BSDF"], out_node.inputs["Surface"])

    tex_dir = config["textures_dir"]
    base_path = os.path.join(tex_dir, "meshy_basecolor.png")
    norm_path = os.path.join(tex_dir, "meshy_normal.png")
    met_path = os.path.join(tex_dir, "meshy_metallic_smoothness.png")

    if os.path.exists(base_path):
        img = bpy.data.images.load(base_path, check_existing=True)
        img.colorspace_settings.name = 'sRGB'
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        tex.location = (-350, 260)
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

    if os.path.exists(met_path):
        img = bpy.data.images.load(met_path, check_existing=True)
        img.colorspace_settings.name = 'Non-Color'
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        tex.location = (-350, 0)
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Metallic"])
        inv = nt.nodes.new("ShaderNodeInvert")
        inv.location = (-120, -120)
        nt.links.new(tex.outputs["Alpha"], inv.inputs["Color"])
        nt.links.new(inv.outputs["Color"], bsdf.inputs["Roughness"])

    if os.path.exists(norm_path):
        img = bpy.data.images.load(norm_path, check_existing=True)
        img.colorspace_settings.name = 'Non-Color'
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        tex.location = (-350, -280)
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nmap.location = (-120, -280)
        nt.links.new(tex.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])

    mesh_obj.data.materials.clear()
    mesh_obj.data.materials.append(mat)
    return mat


def clean_character_mesh(config):
    """Import source FBX, strip old rig/actions, clean mesh, scale to target_height, save orig_blend."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=config["source_fbx"])

    # Reset any old armatures to REST before baking/removing so we keep pure T-Pose
    for obj in list(bpy.data.objects):
        if obj.type == 'ARMATURE':
            obj.data.pose_position = 'REST'
    bpy.context.view_layer.update()

    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if not meshes:
        raise RuntimeError(f"No mesh found in {config['source_fbx']}")

    for m in meshes:
        world_mat = m.matrix_world.copy()
        m.parent = None
        m.matrix_world = world_mat
        for mod in list(m.modifiers):
            if mod.type == 'ARMATURE':
                m.modifiers.remove(mod)
        m.vertex_groups.clear()

    for obj in list(bpy.data.objects):
        if obj.type != 'MESH':
            bpy.data.objects.remove(obj, do_unlink=True)
    for act in list(bpy.data.actions):
        bpy.data.actions.remove(act)
    for arm in list(bpy.data.armatures):
        bpy.data.armatures.remove(arm)

    bpy.ops.object.select_all(action='DESELECT')
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()

    mesh_obj = bpy.context.view_layer.objects.active
    mesh_obj.name = config["mesh_name"]
    mesh_obj.data.name = config["mesh_name"] + "_Mesh"

    # Apply rotation & scale first
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    # Remove loose vertices and small disconnected floating islands
    bm = bmesh.new()
    bm.from_mesh(mesh_obj.data)
    bm.verts.ensure_lookup_table()

    # Remove completely loose verts
    for v in [v for v in bm.verts if not v.link_edges]:
        bm.verts.remove(v)
    bm.verts.ensure_lookup_table()

    # Keep only significant connected components (>1% of total verts)
    visited = set()
    components = []
    for v in bm.verts:
        if v.index in visited:
            continue
        stack = [v]
        comp = []
        visited.add(v.index)
        while stack:
            cur = stack.pop()
            comp.append(cur)
            for e in cur.link_edges:
                ov = e.other_vert(cur)
                if ov.index not in visited:
                    visited.add(ov.index)
                    stack.append(ov)
        components.append(comp)

    total_v = len(bm.verts)
    for comp in components:
        if len(comp) < max(8, int(total_v * 0.01)):
            for v in comp:
                if v.is_valid:
                    bm.verts.remove(v)

    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh_obj.data)
    bm.free()
    mesh_obj.data.update()

    # Ground feet at Z = 0.000 and scale cleanly to target_height
    coords = [mesh_obj.matrix_world @ v.co for v in mesh_obj.data.vertices]
    min_z = min(c.z for c in coords)
    max_z = max(c.z for c in coords)
    cur_h = max(1e-5, max_z - min_z)
    scale_fac = config["target_height"] / cur_h

    for v in mesh_obj.data.vertices:
        w = mesh_obj.matrix_world @ v.co
        w.z = (w.z - min_z) * scale_fac
        w.x = w.x * scale_fac
        w.y = w.y * scale_fac
        v.co = mesh_obj.matrix_world.inverted() @ w
    mesh_obj.location = (0.0, 0.0, 0.0)
    mesh_obj.rotation_euler = (0.0, 0.0, 0.0)
    mesh_obj.scale = (1.0, 1.0, 1.0)
    mesh_obj.data.update()

    setup_character_material(mesh_obj, config)
    os.makedirs(os.path.dirname(config["orig_blend"]), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=config["orig_blend"])

    tris = sum(max(0, len(p.vertices) - 2) for p in mesh_obj.data.polygons)
    print(f"Cleaned {config['mesh_name']}: height={config['target_height']:.3f}m, tris={tris}")
    return mesh_obj


def decimate_character_mesh(config):
    """Selectively decimate mesh toward target_tris, preserving head/face/hair above hair_z_threshold."""
    mesh_obj = bpy.data.objects[config["mesh_name"]]
    bpy.context.view_layer.objects.active = mesh_obj

    z_thresh = config.get("hair_z_threshold", 1.48)
    target_tris = config.get("target_tris", 30000)
    joints = config.get("joints") or {}
    lw_x = joints["LeftHand"][0][0] if "LeftHand" in joints else 999.0
    rw_x = joints["RightHand"][0][0] if "RightHand" in joints else -999.0

    def _is_preserved(co):
        return co.z >= z_thresh or co.x >= lw_x - 0.015 or co.x <= rw_x + 0.015

    head_verts = {v.index for v in mesh_obj.data.vertices if _is_preserved(v.co)}
    head_tris = 0
    body_tris = 0
    for poly in mesh_obj.data.polygons:
        ptris = max(0, len(poly.vertices) - 2)
        if any(vi in head_verts for vi in poly.vertices):
            head_tris += ptris
        else:
            body_tris += ptris

    # Note: Blender's Decimate modifier ratio targets total mesh reduction while
    # the vertex_group restricts which faces/edges are allowed to collapse.
    total_tris = head_tris + body_tris
    if total_tris <= target_tris:
        print(f"No decimation needed: tris={total_tris}")
        return mesh_obj

    vg = mesh_obj.vertex_groups.get("BodyDecimate") or mesh_obj.vertex_groups.new(name="BodyDecimate")
    body_indices = [v.index for v in mesh_obj.data.vertices if not _is_preserved(v.co)]
    vg.add(body_indices, 1.0, 'REPLACE')

    ratio = max(0.15, min(1.0, target_tris / max(1, total_tris)))

    mod = mesh_obj.modifiers.new(name="SelectiveDecimate", type='DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.vertex_group = "BodyDecimate"
    mod.ratio = ratio
    bpy.ops.object.modifier_apply(modifier=mod.name)

    if "BodyDecimate" in mesh_obj.vertex_groups:
        mesh_obj.vertex_groups.remove(mesh_obj.vertex_groups["BodyDecimate"])

    # Mild secondary pass on whole mesh if still > target_tris + 1500
    cur_tris = sum(max(0, len(p.vertices) - 2) for p in mesh_obj.data.polygons)
    if cur_tris > target_tris + 1500:
        mod2 = mesh_obj.modifiers.new(name="BalanceDecimate", type='DECIMATE')
        mod2.decimate_type = 'COLLAPSE'
        mod2.ratio = target_tris / cur_tris
        bpy.ops.object.modifier_apply(modifier=mod2.name)
        cur_tris = sum(max(0, len(p.vertices) - 2) for p in mesh_obj.data.polygons)

    # Ensure outward normals after decimation
    bm = bmesh.new()
    bm.from_mesh(mesh_obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh_obj.data)
    bm.free()
    mesh_obj.data.update()

    print(f"Decimated {config['mesh_name']}: head_tris~={head_tris}, final_tris={cur_tris}")
    return mesh_obj


def _seg_dist_and_t(p, a, b):
    """Return (closest_distance, clamped_t [0..1], unclamped_t) from point p to segment ab."""
    ab = b - a
    len_sq = ab.length_squared
    if len_sq < 1e-10:
        return (p - a).length, 0.0, 0.0
    raw_t = (p - a).dot(ab) / len_sq
    t = max(0.0, min(1.0, raw_t))
    closest = a + ab * t
    return (p - closest).length, t, raw_t


def _smoothstep01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def detect_hand_fingers(mesh_obj, hand_head, hand_tail, sign):
    """Detect real finger chains (Thumb, Index, Middle, Ring, Little) from T-Pose mesh geometry."""
    mw = mesh_obj.matrix_world
    verts = mesh_obj.data.vertices
    co = {v.index: (mw @ v.co) for v in verts}

    h_head = Vector(hand_head)
    h_tail = Vector(hand_tail)
    wrist_along = h_head.x * sign

    hand_indices = {
        v.index for v in verts
        if co[v.index].x * sign >= wrist_along - 0.020
        and abs(co[v.index].y - h_head.y) < 0.22
        and abs(co[v.index].z - h_head.z) < 0.18
    }
    if not hand_indices:
        hand_indices = {
            v.index for v in verts
            if co[v.index].x * sign >= wrist_along - 0.01
        }

    adj = {vi: [] for vi in hand_indices}
    for e in mesh_obj.data.edges:
        a, b = e.vertices[0], e.vertices[1]
        if a in adj and b in adj:
            adj[a].append(b)
            adj[b].append(a)

    max_along = max((co[vi].x * sign for vi in hand_indices), default=wrist_along + 0.20)
    hand_len = max(0.08, max_along - wrist_along)

    # Scan longitudinal slices past webbing to isolate the 4 parallel fingers
    best_comps = []
    best_thresh = wrist_along + 0.68 * hand_len
    for t in (0.58, 0.62, 0.66, 0.70, 0.74, 0.78, 0.82):
        thresh = wrist_along + t * hand_len
        active = {vi for vi in hand_indices if co[vi].x * sign >= thresh}
        visited = set()
        comps = []
        for start in active:
            if start in visited:
                continue
            stack = [start]
            visited.add(start)
            comp = []
            while stack:
                cur = stack.pop()
                comp.append(cur)
                for nb in adj[cur]:
                    if nb in active and nb not in visited:
                        visited.add(nb)
                        stack.append(nb)
            if len(comp) >= 6:
                comps.append(comp)
        if len(comps) == 4:
            best_comps = comps
            best_thresh = thresh
            break
        if len(comps) <= 4 and len(comps) > len(best_comps):
            best_comps = comps
            best_thresh = thresh

    # Order components strictly from thumb side (-Y) to pinky side (+Y)
    best_comps.sort(key=lambda c: sum(co[vi].y for vi in c) / len(c))

    fingers = {}
    comp_map = {}
    finger_u = (0.0, 0.46, 0.76, 1.0)
    knuckle_ratios = {"Index": 0.34, "Middle": 0.36, "Ring": 0.34, "Little": 0.29}
    main_names = ["Index", "Middle", "Ring", "Little"]

    if len(best_comps) == 4:
        for fname, comp in zip(main_names, best_comps):
            for vi in comp:
                comp_map[vi] = fname
            tip_along = max(co[vi].x * sign for vi in comp)
            tip_vs = [vi for vi in comp if co[vi].x * sign >= tip_along - 0.012]
            tip_y = sum(co[vi].y for vi in tip_vs) / len(tip_vs)
            tip_z = sum(co[vi].z for vi in tip_vs) / len(tip_vs)
            tip_pt = Vector((sign * (tip_along - 0.004), tip_y, tip_z))

            web_vs = [vi for vi in comp if co[vi].x * sign <= best_thresh + 0.014] or comp
            web_y = sum(co[vi].y for vi in web_vs) / len(web_vs)
            web_z = sum(co[vi].z for vi in web_vs) / len(web_vs)

            k_along = min(best_thresh - 0.010, tip_along - knuckle_ratios[fname] * hand_len)
            k_along = max(wrist_along + 0.45 * hand_len, k_along)
            k_y = 0.90 * web_y + 0.10 * h_head.y
            k_z = 0.80 * web_z + 0.20 * h_tail.z
            k_pt = Vector((sign * k_along, k_y, k_z))

            pts = [k_pt.lerp(tip_pt, u) for u in finger_u]
            fingers[fname] = pts
        index_min_y = min(co[vi].y for vi in best_comps[0])
    else:
        # Proportional fallback for fused or low-detail future hand meshes
        distal_vs = [vi for vi in hand_indices if co[vi].x * sign >= wrist_along + 0.55 * hand_len] or list(hand_indices)
        min_y = min(co[vi].y for vi in distal_vs)
        max_y = max(co[vi].y for vi in distal_vs)
        mean_z = sum(co[vi].z for vi in distal_vs) / len(distal_vs)
        span_y = max(0.05, max_y - min_y)
        tip_fracs = (0.94, 1.00, 0.93, 0.82)
        for i, fname in enumerate(main_names):
            fy = min_y + span_y * (0.14 + 0.24 * i)
            k_pt = Vector((sign * (wrist_along + 0.50 * hand_len), fy, mean_z))
            t_pt = Vector((sign * (wrist_along + tip_fracs[i] * hand_len), fy, mean_z))
            fingers[fname] = [k_pt.lerp(t_pt, u) for u in finger_u]
        index_min_y = min_y + 0.05 * span_y

    # Detect ventral (-Y) Thumb cluster proximal to main fingers
    thumb_vs = [
        vi for vi in hand_indices
        if 0.12 * hand_len <= (co[vi].x * sign - wrist_along) <= 0.64 * hand_len
        and co[vi].y < index_min_y - 0.005
    ]
    thumb_u = (0.0, 0.40, 0.73, 1.0)
    if len(thumb_vs) >= 6:
        scored = sorted(
            thumb_vs,
            key=lambda vi: (co[vi].x * sign - wrist_along) * 0.55 + (index_min_y - co[vi].y),
            reverse=True,
        )
        top_n = max(4, min(10, len(scored) // 8))
        tip_cluster = scored[:top_n]
        raw_tip = Vector((
            sum(co[vi].x for vi in tip_cluster) / len(tip_cluster),
            sum(co[vi].y for vi in tip_cluster) / len(tip_cluster),
            sum(co[vi].z for vi in tip_cluster) / len(tip_cluster),
        ))
        base_vs = sorted(
            thumb_vs,
            key=lambda vi: co[vi].x * sign - wrist_along,
        )[:max(4, min(10, len(thumb_vs) // 5))]
        if base_vs:
            raw_base = Vector((
                sum(co[vi].x for vi in base_vs) / len(base_vs),
                sum(co[vi].y for vi in base_vs) / len(base_vs),
                sum(co[vi].z for vi in base_vs) / len(base_vs),
            ))
            # Pull base slightly inward into the thenar pad
            t_base = Vector((
                raw_base.x + sign * 0.004,
                0.60 * raw_base.y + 0.40 * (index_min_y - 0.002),
                0.70 * raw_base.z + 0.30 * (h_tail.z - 0.006),
            ))
        else:
            t_base = Vector((sign * (wrist_along + 0.20 * hand_len), index_min_y - 0.008, h_tail.z - 0.008))
        axis = raw_tip - t_base
        if axis.length > 0.01:
            t_tip = raw_tip - axis.normalized() * 0.004
        else:
            t_tip = raw_tip
        if (t_tip - t_base).length < 0.24 * hand_len:
            t_base = Vector((
                sign * (wrist_along + 0.20 * hand_len),
                0.5 * (t_tip.y + index_min_y),
                h_tail.z - 0.008,
            ))
        fingers["Thumb"] = [t_base.lerp(t_tip, u) for u in thumb_u]
    else:
        t_base = Vector((sign * (wrist_along + 0.18 * hand_len), h_head.y - 0.025, h_tail.z - 0.010))
        t_tip = Vector((sign * (wrist_along + 0.50 * hand_len), h_head.y - 0.075, h_tail.z - 0.025))
        fingers["Thumb"] = [t_base.lerp(t_tip, u) for u in thumb_u]

    return {
        "fingers": fingers,
        "comp_map": comp_map,
        "hand_indices": hand_indices,
        "wrist_along": wrist_along,
        "hand_len": hand_len,
        "split_along": best_thresh,
        "index_min_y": index_min_y,
    }


def _populate_finger_bones_edit_mode(arm_obj, mesh_obj):
    """Create or reposition the 30 deformable finger bones inside arm_obj (EDIT mode expected)."""
    eb = arm_obj.data.edit_bones
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        hand_bone = eb[f"{side}Hand"]
        if mesh_obj is not None:
            det = detect_hand_fingers(mesh_obj, hand_bone.head.copy(), hand_bone.tail.copy(), sign)
            finger_pts = det["fingers"]
        else:
            hx, hy, hz = hand_bone.tail.x, hand_bone.tail.y, hand_bone.tail.z
            finger_pts = {}
            for idx, fname in enumerate(FINGER_NAMES):
                y_off = (-0.026 + (idx - 1) * 0.022) if fname != "Thumb" else -0.055
                z_off = -0.012 if fname == "Thumb" else 0.0
                sx = hx - sign * (0.025 if fname == "Thumb" else 0.0)
                seg_len = 0.018 if fname == "Thumb" else 0.022
                finger_pts[fname] = [
                    Vector((sx + sign * i * seg_len, hy + y_off, hz + z_off)) for i in range(4)
                ]

        for fname in FINGER_NAMES:
            pts = finger_pts[fname]
            prev = hand_bone
            for s_idx, seg in enumerate(FINGER_SEGMENTS):
                bname = f"{side}{fname}{seg}"
                fb = eb.get(bname) or eb.new(bname)
                fb.head = pts[s_idx]
                fb.tail = pts[s_idx + 1]
                fb.parent = prev
                fb.use_connect = (s_idx > 0)
                fb.use_deform = True
                prev = fb


def assign_hand_finger_weights(mesh_obj, arm_obj, config=None):
    """Assign clean per-phalanx weights with zero cross-finger bleeding and palm/wrist preservation."""
    mw = mesh_obj.matrix_world
    verts = mesh_obj.data.vertices

    finger_vg = {}
    for side in ("Left", "Right"):
        for fname in FINGER_NAMES:
            for seg in FINGER_SEGMENTS:
                gname = f"{side}{fname}{seg}"
                vg = mesh_obj.vertex_groups.get(gname) or mesh_obj.vertex_groups.new(name=gname)
                finger_vg[gname] = vg

    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        hand_bone = arm_obj.data.bones[f"{side}Hand"]
        hand_vg = mesh_obj.vertex_groups.get(f"{side}Hand") or mesh_obj.vertex_groups.new(name=f"{side}Hand")
        det = detect_hand_fingers(mesh_obj, hand_bone.head_local, hand_bone.tail_local, sign)
        fingers = det["fingers"]
        comp_map = det["comp_map"]
        hand_indices = det["hand_indices"]
        wrist_along = det["wrist_along"]
        index_min_y = det["index_min_y"]

        side_finger_vgs = [
            finger_vg[f"{side}{fname}{seg}"]
            for fname in FINGER_NAMES
            for seg in FINGER_SEGMENTS
        ]
        for vg in side_finger_vgs:
            vg.remove(list(hand_indices))

        # Keep smooth wrist blend below +0.015; assign everything distal cleanly
        distal_hand = [vi for vi in hand_indices if (mw @ verts[vi].co).x * sign >= wrist_along + 0.015]
        for vg in mesh_obj.vertex_groups:
            if vg.name != f"{side}Hand":
                vg.remove(distal_hand)

        thumb_pts = fingers["Thumb"]
        thumb_u = (0.0, 0.40, 0.73, 1.0)
        finger_u = (0.0, 0.46, 0.76, 1.0)

        def apply_chain_weights(vi, fname, t_val, joints_u):
            g_prox = finger_vg[f"{side}{fname}Proximal"]
            g_int = finger_vg[f"{side}{fname}Intermediate"]
            g_dist = finger_vg[f"{side}{fname}Distal"]
            u1, u2 = joints_u[1], joints_u[2]

            if t_val <= -0.09:
                hand_vg.add([vi], 1.0, 'REPLACE')
            elif t_val < 0.12:
                w = _smoothstep01((t_val + 0.09) / 0.21)
                hand_vg.add([vi], 1.0 - w, 'REPLACE')
                g_prox.add([vi], w, 'REPLACE')
            elif t_val <= u1 - 0.11:
                hand_vg.remove([vi])
                g_prox.add([vi], 1.0, 'REPLACE')
            elif t_val < u1 + 0.11:
                w = _smoothstep01((t_val - (u1 - 0.11)) / 0.22)
                hand_vg.remove([vi])
                g_prox.add([vi], 1.0 - w, 'REPLACE')
                g_int.add([vi], w, 'REPLACE')
            elif t_val <= u2 - 0.09:
                hand_vg.remove([vi])
                g_int.add([vi], 1.0, 'REPLACE')
            elif t_val < u2 + 0.09:
                w = _smoothstep01((t_val - (u2 - 0.09)) / 0.18)
                hand_vg.remove([vi])
                g_int.add([vi], 1.0 - w, 'REPLACE')
                g_dist.add([vi], w, 'REPLACE')
            else:
                hand_vg.remove([vi])
                g_dist.add([vi], 1.0, 'REPLACE')

        for vi in distal_hand:
            p = mw @ verts[vi].co
            d_thumb, _, raw_t_thumb = _seg_dist_and_t(p, thumb_pts[0], thumb_pts[3])

            best_main = None
            best_d_main = 1e9
            best_raw_main = 0.0
            for fname in ("Index", "Middle", "Ring", "Little"):
                pts = fingers[fname]
                d_f, _, raw_t = _seg_dist_and_t(p, pts[0], pts[3])
                if d_f < best_d_main:
                    best_d_main = d_f
                    best_main = fname
                    best_raw_main = raw_t

            is_thumb = (
                raw_t_thumb >= -0.05
                and d_thumb < 0.028
                and d_thumb < best_d_main * 1.12
                and vi not in comp_map
            )
            if is_thumb:
                apply_chain_weights(vi, "Thumb", raw_t_thumb, thumb_u)
                continue

            if vi in comp_map:
                chosen = comp_map[vi]
                pts = fingers[chosen]
                _, _, raw_t = _seg_dist_and_t(p, pts[0], pts[3])
                apply_chain_weights(vi, chosen, raw_t, finger_u)
            else:
                if best_raw_main <= -0.07:
                    hand_vg.add([vi], 1.0, 'REPLACE')
                else:
                    apply_chain_weights(vi, best_main, best_raw_main, finger_u)


def update_existing_rig_with_fingers(config):
    """Upgrade an existing character .blend in-place to a 52-bone deformable finger rig."""
    bpy.ops.wm.open_mainfile(filepath=config["work_blend"])
    arm_obj = bpy.data.objects[config["armature_name"]]
    mesh_obj = bpy.data.objects[config["mesh_name"]]

    bpy.ops.object.mode_set(mode='OBJECT')
    arm_obj.data.pose_position = 'POSE'
    if arm_obj.animation_data:
        arm_obj.animation_data.action = None
    reset_pose(arm_obj)
    bpy.context.view_layer.update()

    bpy.ops.object.select_all(action='DESELECT')
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='EDIT')

    _populate_finger_bones_edit_mode(arm_obj, mesh_obj)

    bpy.ops.armature.select_all(action='SELECT')
    bpy.ops.armature.calculate_roll(type='GLOBAL_POS_Z')
    bpy.ops.object.mode_set(mode='OBJECT')

    assign_hand_finger_weights(mesh_obj, arm_obj, config)

    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)

    deform_count = sum(1 for b in arm_obj.data.bones if b.use_deform)
    print(f"Updated rig {config['armature_name']}: total={len(arm_obj.data.bones)}, deform={deform_count}")
    bpy.ops.wm.save_as_mainfile(filepath=config["work_blend"])
    return arm_obj, mesh_obj


def create_humanoid_rig(config):
    """Build 52-bone Unity Humanoid rig (all 52 bones deformable with detected phalanges)."""
    joints = config["joints"]
    if not joints:
        raise ValueError(f"Joints dictionary is empty for {config['name']}")

    arm_name = config["armature_name"]
    mesh_obj = bpy.data.objects.get(config["mesh_name"])
    if arm_name in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[arm_name], do_unlink=True)
    if arm_name in bpy.data.armatures:
        bpy.data.armatures.remove(bpy.data.armatures[arm_name])

    arm_data = bpy.data.armatures.new(arm_name)
    arm_data.display_type = 'STICK'
    arm_obj = bpy.data.objects.new(arm_name, arm_data)
    bpy.context.collection.objects.link(arm_obj)
    arm_obj.location = (0.0, 0.0, 0.0)
    arm_obj.rotation_euler = (0.0, 0.0, 0.0)
    arm_obj.scale = (1.0, 1.0, 1.0)
    arm_obj.show_in_front = True

    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_data.edit_bones

    created = {}
    for bname, (head, tail) in joints.items():
        bone = eb.new(bname)
        bone.head = Vector(head)
        bone.tail = Vector(tail)
        bone.use_deform = True
        created[bname] = bone

    for bname, (pname, connect) in BONE_PARENTS.items():
        if pname is not None and pname in created:
            created[bname].parent = created[pname]
            created[bname].use_connect = connect

    _populate_finger_bones_edit_mode(arm_obj, mesh_obj)

    # Align bone rolls cleanly (recalculate Z+ for spine/legs, X/Z consistent for arms/fingers)
    bpy.ops.armature.select_all(action='SELECT')
    bpy.ops.armature.calculate_roll(type='GLOBAL_POS_Z')

    bpy.ops.object.mode_set(mode='OBJECT')
    print(f"Created rig {arm_name}: total={len(arm_data.bones)}, deform={sum(1 for b in arm_data.bones if b.use_deform)}")
    return arm_obj


def bind_and_refine_weights(config):
    """Bind mesh with automatic weights, strip cross-body leaks, refine hands, limit to 4, normalize."""
    mesh_obj = bpy.data.objects[config["mesh_name"]]
    arm_obj = bpy.data.objects[config["armature_name"]]

    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')

    center_x = config.get("center_x", 0.0)
    neck_z = config["joints"]["Neck"][1][2]
    hip_z = config["joints"]["Hips"][0][2]

    # Clean non-deforming or stray vertex groups
    deform_names = {b.name for b in arm_obj.data.bones if b.use_deform}
    for vg in list(mesh_obj.vertex_groups):
        if vg.name not in deform_names:
            mesh_obj.vertex_groups.remove(vg)

    # Remove cross-leg leakage below hip height and shoulder/neck leakage into upper head
    left_leg_groups = {mesh_obj.vertex_groups[n] for n in ("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes") if n in mesh_obj.vertex_groups}
    right_leg_groups = {mesh_obj.vertex_groups[n] for n in ("RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes") if n in mesh_obj.vertex_groups}
    upper_body_groups = {mesh_obj.vertex_groups[n] for n in ("LeftShoulder", "RightShoulder", "LeftUpperArm", "RightUpperArm", "UpperChest", "Chest") if n in mesh_obj.vertex_groups}
    head_group = mesh_obj.vertex_groups.get("Head")

    for v in mesh_obj.data.vertices:
        # Prevent cross-leg bleeding below pelvis (tight seam split below crotch)
        if v.co.z < hip_z - 0.04:
            seam_margin = 0.002 if v.co.z < hip_z - 0.14 else 0.015
            if v.co.x > center_x + seam_margin:
                for g in right_leg_groups:
                    g.remove([v.index])
            elif v.co.x < center_x - seam_margin:
                for g in left_leg_groups:
                    g.remove([v.index])
        # Prevent torso/shoulders from pulling face/crown vertices
        if v.co.z > neck_z + 0.05 and head_group is not None:
            for g in upper_body_groups:
                g.remove([v.index])
            if v.co.z > neck_z + 0.08:
                head_group.add([v.index], 1.0, 'REPLACE')

    assign_hand_finger_weights(mesh_obj, arm_obj, config)

    # Limit to max 4 bone influences per vertex and normalize
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)

    print(f"Weights bound and refined for {config['mesh_name']} (max 4 influences, normalized).")


def render_pose_checks(arm_obj, mesh_obj, out_dir, prefix="test", style="heavy_brawler"):
    """Render 3 extreme weight-validation poses (high kick, deep squat, punch) to PNGs."""
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.image_settings.file_format = 'PNG'

    cam_data = bpy.data.cameras.get("CheckCam") or bpy.data.cameras.new("CheckCam")
    cam_obj = bpy.data.objects.get("CheckCam") or bpy.data.objects.new("CheckCam", cam_data)
    if cam_obj.name not in scene.collection.objects:
        scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    # 3/4 side-front camera framing whole character
    cam_obj.location = Vector((-1.95, -2.55, 1.08))
    direction = Vector((0.0, 0.0, 0.90)) - cam_obj.location
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

    arm_obj.data.pose_position = 'POSE'
    if arm_obj.animation_data:
        arm_obj.animation_data.action = None

    idle0 = get_idle_stance(0.0, style=style)
    poses = {
        "high_kick": merge_pose(idle0, {
            "HipsOffset":   (0.0, 0.0, -0.02),
            "Hips":         (-16.0, 8.0, -12.0),
            "Spine":        (-10.0, 0.0, 5.0),
            "LeftUpperLeg": (-108.0, -8.0, 8.0),
            "LeftLowerLeg": (4.0, 0.0, 0.0),
            "LeftFoot":     (22.0, 0.0, 0.0),
        }),
        "deep_squat": merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.40),
            "Hips":          (16.0, 0.0, 18.0),
            "Spine":         (12.0, 0.0, 4.0),
            "LeftUpperLeg":  (-100.0, -8.0, 10.0),
            "LeftLowerLeg":  (108.0, 0.0, 0.0),
            "LeftFoot":      (-8.0, 0.0, 0.0),
            "RightUpperLeg": (-96.0, 8.0, -10.0),
            "RightLowerLeg": (106.0, 0.0, 0.0),
            "RightFoot":     (-10.0, 0.0, 0.0),
        }),
        "punch": merge_pose(idle0, {
            "HipsOffset":    (0.0, 0.0, -0.075),
            "Hips":          (8.0, 0.0, 14.0),
            "Spine":         (8.0, 0.0, 12.0),
            "Chest":         (8.0, 0.0, 14.0),
            "UpperChest":    (6.0, 0.0, 10.0),
            "Neck":          (-8.0, 0.0, -25.0),
            "Head":          (-8.0, 0.0, -25.0),
            "RightShoulder": (0.0, -6.0, 14.0),
            "RightUpperArm": (-10.0, 0.0, 32.0),
            "RightLowerArm": (0.0, 42.0, 4.0),
            "RightHand":     (-22.0, 40.0, 0.0),
        }),
    }

    paths = {}
    for pname, pose_dict in poses.items():
        apply_pose_dict(arm_obj, pose_dict)
        bpy.context.view_layer.update()
        out_path = os.path.join(out_dir, f"{prefix}_{pname}.png")
        scene.render.filepath = out_path
        bpy.ops.render.render(write_still=True)
        paths[pname] = out_path

    reset_pose(arm_obj)
    bpy.context.view_layer.update()
    return paths


def build_character_base(char_name="Tiago"):
    config = CHARACTER_CONFIGS[char_name]
    clean_character_mesh(config)
    decimate_character_mesh(config)
    create_humanoid_rig(config)
    bind_and_refine_weights(config)
    os.makedirs(os.path.dirname(config["work_blend"]), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=config["work_blend"])
    return config


def write_frame_data_doc(config):
    """Write Docs/FrameData_<Name>.md automatically from weight_class and fighting_style."""
    name = config["name"]
    weight_class, style, fd = get_fighter_framedata(config)
    weight_kg = config.get("weight_kg", 92.0)
    height_m = config["target_height"]
    archetype_label = config.get("archetype_label", style)

    weight_pt = {"light": "Leve (<= 75 kg)", "medium": "Médio (75–90 kg)", "heavy": "Pesado (> 90 kg)"}[weight_class]
    style_notes = {
        "heavy_brawler": {
            "LightPunch": "Jab/direto pesado com forte rotação de ombro e tronco (`LeftHand`)",
            "HeavyPunch": "Cruzado/gancho potente com grande preparação de quadril e extensão total (`RightHand`)",
            "LightKick": "Pisão/chute baixo pesado na altura da canela/joelho (`LeftFoot`)",
            "HeavyKick": "Chutão frontal/lateral empurrando com o calcanhar e recuo sólido (`LeftFoot`)",
            "Special": "Especial **Martelo do Asfalto**: preparação profunda com as duas mãos e martelada dupla esmagadora (`RightHand`/`LeftHand`)",
            "Idle": "Base plantada e baixa, ombros firmes, guarda alta e fechada protegendo queixo/têmpora, respiração pesada",
            "Walk": "Passada firme e pesada mantendo guarda dupla fechada",
            "BlockStand": "Guarda dupla fechada e imóvel mantida até o último frame",
            "Victory": "Postura dominante erguendo e fechando o punho com firmeza até o último frame",
        },
        "street_scrappy": {
            "LightPunch": "Soco reto apressado e meio torto sem alinhamento perfeito de ombro (`LeftHand`)",
            "HeavyPunch": "Mata-cobra aberto e largo inclinando o tronco junto com o golpe (`RightHand`)",
            "LightKick": "Bico/chute baixo improvisado de rua (`LeftFoot`)",
            "HeavyKick": "Chute alto desengonçado erguendo o braço oposto para equilíbrio (`LeftFoot`)",
            "Special": "Especial **Sequência de Rua**: cruzado apressado de esquerda emendando em mata-cobra descendente brutal (`LeftHand`+`RightHand`)",
            "Idle": "Base aberta e assimétrica, guarda mais baixa e solta, leve ginga irregular de rua",
            "Walk": "Passada solta e menos cadenciada, braços balançando fora da guarda padrão",
            "BlockStand": "Encolhe os ombros e cruza os antebraços em frente ao rosto de forma instintiva até o último frame",
            "Victory": "Comemoração descontraída de rua erguendo o braço e mantendo a pose até o último frame",
        },
        "technical_striker": {
            "LightPunch": "Jab/direto marcial rápido com guarda traseira fechada (`LeftHand`)",
            "HeavyPunch": "Golpe penetrante/cruzado firme com giro limpo de quadril (`RightHand`)",
            "LightKick": "Chute frontal/snap rápido na altura baixa/média (`LeftFoot`)",
            "HeavyKick": "Chute alto circular técnico com rotação de quadril e guarda fechada (`LeftFoot`)",
            "Special": "Especial **Golpe Sombrio**: avanço baixo em postura fechada seguido de estocada/palma perfurante central (`RightHand`)",
            "Idle": "Guarda marcial disciplinada e fechada (mão da frente alta, mão de trás junto ao peito/queixo)",
            "Walk": "Deslocamento técnico compacto mantendo a guarda marcial travada",
            "BlockStand": "Bloqueio marcial peek-a-boo fechado até o último frame",
            "Victory": "Postura firme e disciplinada de sentinela/artes marciais até o último frame",
        },
        "agile_striker": {
            "LightPunch": "Jab veloz com leve esquiva de tronco (`LeftHand`)",
            "HeavyPunch": "Cruzado rápido com passo firme para frente (`RightHand`)",
            "LightKick": "Chute baixo/médio veloz e elástico (`LeftFoot`)",
            "HeavyKick": "Chute alto dinâmico/explosivo com contrapeso do braço (`LeftFoot`)",
            "Special": "Especial **Tornado Relâmpago**: giro ágil com chute alto explosivo em arco (`LeftFoot`)",
            "Idle": "Postura atlética leve com ginga/bounce ágil e joelhos elásticos",
            "Walk": "Passada leve e saltitante mantendo guarda ágil",
            "BlockStand": "Guarda alta compacta mantida até o último frame",
            "Victory": "Comemoração vibrante e energética com punho erguido até o último frame",
        },
    }[style]

    common_attack_notes = {
        "CrouchKick": "Rasteira agachada baixa varrendo o chão com a perna estendida à frente (`LeftFoot`, `HitHeight.Low`, derruba)",
        "JumpAttack": "Voadora aérea descendente em diagonal (`LeftFoot`, `HitHeight.Overhead`, quebra guarda agachada)",
        "Throw": "Agarrão de curto alcance com ambas as mãos (`relaxed_grip`) seguido de arremesso firme de ombro/tronco (`Unblockable`)",
    }

    def fmt_row(mname):
        s, a, r = fd[mname]
        tot = s + a + r
        dur_str = f"{tot/60.0:.3f}".replace(".", ",")
        note = style_notes.get(mname) or common_attack_notes[mname]
        return f"| **{mname}** | {s} | {a} | {r} | {tot} | {dur_str} s | {s+1}–{s+a} | {note} |"

    doc_path = os.path.join(PROJECT_ROOT, "Docs", f"FrameData_{name}.md")
    lines = [
        f"# Frame Data — {name} (`{style}`)",
        "",
        f"- **Altura:** `{height_m:.2f} m` | **Peso:** `{weight_kg:.0f} kg` | **Faixa de Peso:** `{weight_pt}` | **Arquétipo:** `{archetype_label}`".replace(".", ",", 1),
        "- **Taxa de Quadros:** **`60 fps`** (`1 frame = 1 tick de simulação = 16,667 ms`)",
        f"- **Rig:** Humanoid (`{config['armature_name']}`, `52` ossos, escala `1.0`, sem *root motion*)",
        f"- **FBX:** `Assets/UnDFight/Art/Models/{name}/{name}.fbx`",
        "",
        "---",
        "",
        "## Golpes (Ataques)",
        "",
        "| Golpe | Startup (frames) | Active (frames) | Recovery (frames) | Total (frames) | Duração (s) | Janela Ativa (frames 1-based) | Notas |",
        "|---|---:|---:|---:|---:|---:|---|---|",
        fmt_row("LightPunch"),
        fmt_row("HeavyPunch"),
        fmt_row("LightKick"),
        fmt_row("HeavyKick"),
        fmt_row("CrouchKick"),
        fmt_row("JumpAttack"),
        fmt_row("Throw"),
        fmt_row("Special"),
        "",
        "---",
        "",
        "## Movimentação, Defesa e Estados",
        "",
        "| Clipe | Frames | Duração (60 fps) | Loop | Root Motion | Observações |",
        "|---|---:|---:|:---:|:---:|---|",
        f"| **Idle** | 60 (1–61) | 1,000 s | Sim | Não | {style_notes['Idle']} |",
        f"| **WalkForward** | 48 (1–49) | 0,800 s | Sim | Não | {style_notes['Walk']} (avanço) |",
        f"| **WalkBackward** | 48 (1–49) | 0,800 s | Sim | Não | {style_notes['Walk']} (recuo) |",
        "| **DashForward** | 14 (1–15) | 0,233 s | Não | Não | Impulso explosivo para frente com inclinação de tronco, guarda firme e retorno ao `Idle` (cancelável após tick 8) |",
        "| **DashBackward** | 16 (1–17) | 0,267 s | Não | Não | Recuo elástico rápido afastando tronco/cabeça para trás e amortecendo na base (evasão nos 5 primeiros ticks) |",
        "| **Jump** | 36 (1–37) | 0,600 s | Não | Não | Antecipação, elevação vertical local e pouso retornando ao `Idle` |",
        "| **Crouch** | 30 (1–31) | 0,500 s | Não | Não | Agachamento em guarda baixa mantido até o frame 31 enquanto segurado |",
        f"| **BlockStand** | 24 (1–25) | 0,400 s | Não | Não | {style_notes['BlockStand']} |",
        "| **HitReact** | 20 (1–21) | 0,333 s | Não | Não | Recuo de tronco e cabeça ao sofrer golpe, recuperando para `Idle` |",
        "| **KnockDown** | 45 (1–46) | 0,750 s | Não | Não | Desequilíbrio e queda de costas no chão |",
        "| **GetUp** | 40 (1–41) | 0,667 s | Não | Não | Levantamento a partir do chão até `Idle` |",
        f"| **Victory** | 60 (1–61) | 1,000 s | Não | Não | {style_notes['Victory']} |",
        "| **Defeat** | 50 (1–51) | 0,833 s | Não | Não | Queda de joelhos e colapso final no chão |",
        "| **Turn180** | 8 (1–9) | 0,133 s | Não | Não | Pivô de troca de lado em pé: flexão leve nos joelhos, giro de tronco/cabeça, passo curto e retorno ao `Idle` |",
    ]
    os.makedirs(os.path.dirname(doc_path), exist_ok=True)
    with open(doc_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"Wrote {doc_path}")
    return doc_path


def render_style_poses(arm_obj, config, out_dir):
    """Render key action poses (Idle, HeavyPunch, HeavyKick, BlockStand, Victory) from baked actions."""
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.image_settings.file_format = 'PNG'

    cam_data = bpy.data.cameras.get("CheckCam") or bpy.data.cameras.new("CheckCam")
    cam_obj = bpy.data.objects.get("CheckCam") or bpy.data.objects.new("CheckCam", cam_data)
    if cam_obj.name not in scene.collection.objects:
        scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    cam_obj.location = Vector((-2.05, -2.55, 1.05))
    direction = Vector((0.0, 0.0, 0.90)) - cam_obj.location
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

    _, _, fd = get_fighter_framedata(config)
    hp_hit_frame = 1 + fd["HeavyPunch"][0] + 1
    hk_hit_frame = 1 + fd["HeavyKick"][0] + 1
    sp_hit_frame = 1 + fd["Special"][0] + 1

    samples = {
        "Idle": 1,
        "HeavyPunch": hp_hit_frame,
        "HeavyKick": hk_hit_frame,
        "Special": sp_hit_frame,
        "BlockStand": 20,
        "Victory": 50,
    }

    arm_obj.data.pose_position = 'POSE'
    paths = {}
    prefix = config["name"]
    for act_name, frame in samples.items():
        arm_obj.animation_data.action = bpy.data.actions[act_name]
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        out_path = os.path.join(out_dir, f"phase9_{prefix}_{act_name}.png")
        scene.render.filepath = out_path
        bpy.ops.render.render(write_still=True)
        paths[act_name] = out_path

    arm_obj.animation_data.action = None
    reset_pose(arm_obj)
    bpy.context.view_layer.update()
    return paths


def render_hand_closeups(arm_obj, config, out_dir):
    """Render tight close-ups of Left & Right hands in Idle and HeavyPunch for visual inspection."""
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.image_settings.file_format = 'PNG'

    cam_data = bpy.data.cameras.get("HandCam") or bpy.data.cameras.new("HandCam")
    cam_data.lens = 50.0
    cam_obj = bpy.data.objects.get("HandCam") or bpy.data.objects.new("HandCam", cam_data)
    if cam_obj.name not in scene.collection.objects:
        scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    weight_class = config.get("weight_class") or get_weight_class(config.get("weight_kg", 92.0))
    hp_hit_frame = 1 + WEIGHT_CLASS_FRAMEDATA[weight_class]["HeavyPunch"][0] + 1

    checks = [
        ("Idle", 1, "Left", Vector((-0.24, -0.26, 0.10))),
        ("Idle", 1, "Right", Vector((-0.24, -0.26, 0.10))),
        ("HeavyPunch", hp_hit_frame, "Right", Vector((0.20, -0.24, -0.10))),
        ("BlockStand", 20, "Left", Vector((-0.24, -0.26, 0.08))),
    ]

    arm_obj.data.pose_position = 'POSE'
    paths = {}
    prefix = config["name"]
    for act_name, frame, side, cam_offset in checks:
        arm_obj.animation_data.action = bpy.data.actions[act_name]
        scene.frame_set(frame)
        bpy.context.view_layer.update()

        pb_hand = arm_obj.pose.bones[f"{side}Hand"]
        pb_mid = arm_obj.pose.bones[f"{side}MiddleProximal"]
        target = (arm_obj.matrix_world @ pb_hand.head).lerp(arm_obj.matrix_world @ pb_mid.tail, 0.55)

        cam_obj.location = target + cam_offset
        direction = target - cam_obj.location
        cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

        key = f"{act_name}_{side}"
        out_path = os.path.join(out_dir, f"hand_{prefix}_{key}.png")
        scene.render.filepath = out_path
        bpy.ops.render.render(write_still=True)
        paths[key] = out_path

    arm_obj.animation_data.action = None
    reset_pose(arm_obj)
    bpy.context.view_layer.update()
    return paths


def run_animations_and_export(char_name="Lucas", render_dir=None, render_hands_dir=None):
    config = CHARACTER_CONFIGS[char_name]
    arm_obj = bpy.data.objects[config["armature_name"]]
    generate_all_animations(arm_obj, config)
    write_frame_data_doc(config)
    if render_dir:
        render_style_poses(arm_obj, config, render_dir)
    if render_hands_dir:
        render_hand_closeups(arm_obj, config, render_hands_dir)
    bpy.ops.wm.save_as_mainfile(filepath=config["work_blend"])
    export_character_fbx(config)
