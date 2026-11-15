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

CHARACTER_CONFIGS = {
    "Lucas": {
        "name": "Lucas",
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
        "center_x": 0.0,
        "fps": 60,
        "joints": {},
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


def set_bone_world_rot(arm_obj, bone_name, rx=0.0, ry=0.0, rz=0.0):
    """Apply world-space XYZ Euler rotation (degrees) relative to bone rest pose."""
    pb = arm_obj.pose.bones[bone_name]
    r_world = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), 'XYZ').to_matrix()
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
        set_bone_world_rot(arm_obj, bone_name, rot[0], rot[1], rot[2])


def key_all_deform_bones(arm_obj, frame):
    for pb in arm_obj.pose.bones:
        if not pb.bone.use_deform:
            continue  # Skip non-deforming finger bones so fused hands aren't keyed
        pb.rotation_mode = 'QUATERNION'
        pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
        if pb.name == "Hips":
            pb.keyframe_insert(data_path="location", frame=frame)


def build_action_from_keyframes(arm_obj, action_name, keyframes):
    """Create or replace an Action on arm_obj from {frame: pose_dict}."""
    if action_name in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions[action_name])
    act = bpy.data.actions.new(name=action_name)
    act.use_fake_user = True
    if not arm_obj.animation_data:
        arm_obj.animation_data_create()
    arm_obj.animation_data.action = act

    for frame in sorted(keyframes.keys()):
        apply_pose_dict(arm_obj, keyframes[frame])
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
# Fighting Stance Base Pose & 15 Animation Generators (60 FPS, No Root Motion)
# ---------------------------------------------------------------------------

def get_idle_stance(breath=0.0):
    """
    Orthodox fighting stance facing -Y (side-on to 2.5D camera).
    breath in [-1, 1] drives subtle breathing bob & guard float.
    """
    dz = -0.045 + 0.008 * breath
    return {
        "HipsOffset":    (0.0, 0.0, dz),
        "Hips":          (4.0, 0.0, 22.0),
        "Spine":         (5.0 + 1.5 * breath, 0.0, 4.0),
        "Chest":         (4.0 + 1.5 * breath, 0.0, 4.0),
        "UpperChest":    (2.0, 0.0, 2.0),
        "Neck":          (-6.0 - 1.0 * breath, 0.0, -14.0),
        "Head":          (-5.0, 0.0, -16.0),
        # Lead left arm (high guard near cheek)
        "LeftShoulder":  (0.0, 12.0, -12.0),
        "LeftUpperArm":  (-8.0, 52.0 - 2.0 * breath, -58.0),
        "LeftLowerArm":  (12.0, -32.0, -96.0),
        "LeftHand":      (-10.0, 0.0, 12.0),
        # Rear right arm (tight chin guard)
        "RightShoulder": (0.0, -12.0, 12.0),
        "RightUpperArm": (-6.0, -54.0 + 2.0 * breath, 56.0),
        "RightLowerArm": (10.0, 34.0, 102.0),
        "RightHand":     (-10.0, 0.0, -12.0),
        # Balanced stance legs (knees bent, feet planted)
        "LeftUpperLeg":  (-22.0, -4.0, 4.0),
        "LeftLowerLeg":  (28.0, 0.0, 0.0),
        "LeftFoot":      (-6.0, 0.0, 0.0),
        "RightUpperLeg": (18.0, 5.0, -4.0),
        "RightLowerLeg": (24.0, 0.0, 0.0),
        "RightFoot":     (-28.0, 0.0, 0.0),
    }


def merge_pose(base, overrides):
    p = dict(base)
    p.update(overrides)
    return p


def generate_all_animations(arm_obj, config=None):
    set_scene_fps(config["fps"] if config else 60)
    arm_obj.data.pose_position = 'POSE'

    idle0 = get_idle_stance(0.0)
    idle_up = get_idle_stance(1.0)
    idle_dn = get_idle_stance(-1.0)

    animations = {}

    # 1. Idle (Loop: 60 frames = 1.0s)
    animations["Idle"] = {
        1: idle0,
        16: idle_up,
        31: idle0,
        46: idle_dn,
        61: idle0,
    }

    # 2. WalkForward (Loop: 48 frames = 0.8s, zero horizontal root motion)
    wf_a = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.055),
        "Spine":         (8.0, 0.0, 8.0),
        "LeftUpperArm":  (-6.0, 50.0, -54.0),
        "RightUpperArm": (-8.0, -52.0, 50.0),
        "LeftUpperLeg":  (-34.0, -4.0, 4.0),
        "LeftLowerLeg":  (24.0, 0.0, 0.0),
        "LeftFoot":      (8.0, 0.0, 0.0),
        "RightUpperLeg": (24.0, 4.0, -4.0),
        "RightLowerLeg": (20.0, 0.0, 0.0),
        "RightFoot":     (-24.0, 0.0, 0.0),
    })
    wf_mid1 = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.032),
        "Spine":         (6.0, 0.0, 2.0),
        "LeftUpperLeg":  (-8.0, -3.0, 2.0),
        "LeftLowerLeg":  (14.0, 0.0, 0.0),
        "LeftFoot":      (-6.0, 0.0, 0.0),
        "RightUpperLeg": (-22.0, 3.0, -2.0),
        "RightLowerLeg": (52.0, 0.0, 0.0),
        "RightFoot":     (-12.0, 0.0, 0.0),
    })
    wf_b = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.055),
        "Spine":         (8.0, 0.0, -4.0),
        "LeftUpperArm":  (-8.0, 54.0, -62.0),
        "RightUpperArm": (-6.0, -50.0, 62.0),
        "LeftUpperLeg":  (24.0, -4.0, 4.0),
        "LeftLowerLeg":  (20.0, 0.0, 0.0),
        "LeftFoot":      (-24.0, 0.0, 0.0),
        "RightUpperLeg": (-34.0, 4.0, -4.0),
        "RightLowerLeg": (24.0, 0.0, 0.0),
        "RightFoot":     (8.0, 0.0, 0.0),
    })
    wf_mid2 = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.032),
        "Spine":         (6.0, 0.0, 4.0),
        "LeftUpperLeg":  (-22.0, -3.0, 2.0),
        "LeftLowerLeg":  (52.0, 0.0, 0.0),
        "LeftFoot":      (-12.0, 0.0, 0.0),
        "RightUpperLeg": (-8.0, 3.0, -2.0),
        "RightLowerLeg": (14.0, 0.0, 0.0),
        "RightFoot":     (-6.0, 0.0, 0.0),
    })
    animations["WalkForward"] = {
        1: wf_a,
        13: wf_mid1,
        25: wf_b,
        37: wf_mid2,
        49: wf_a,
    }

    # 3. WalkBackward (Loop: 48 frames = 0.8s, defensive retreat, zero horizontal root motion)
    wb_a = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.055),
        "Spine":         (2.0, 0.0, 4.0),
        "LeftUpperLeg":  (18.0, -4.0, 4.0),
        "LeftLowerLeg":  (16.0, 0.0, 0.0),
        "LeftFoot":      (-20.0, 0.0, 0.0),
        "RightUpperLeg": (-26.0, 4.0, -4.0),
        "RightLowerLeg": (28.0, 0.0, 0.0),
        "RightFoot":     (-2.0, 0.0, 0.0),
    })
    wb_mid1 = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.035),
        "LeftUpperLeg":  (-18.0, -3.0, 2.0),
        "LeftLowerLeg":  (46.0, 0.0, 0.0),
        "RightUpperLeg": (-10.0, 3.0, -2.0),
        "RightLowerLeg": (14.0, 0.0, 0.0),
    })
    wb_b = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.055),
        "Spine":         (2.0, 0.0, 6.0),
        "LeftUpperLeg":  (-26.0, -4.0, 4.0),
        "LeftLowerLeg":  (28.0, 0.0, 0.0),
        "LeftFoot":      (-2.0, 0.0, 0.0),
        "RightUpperLeg": (18.0, 4.0, -4.0),
        "RightLowerLeg": (16.0, 0.0, 0.0),
        "RightFoot":     (-20.0, 0.0, 0.0),
    })
    wb_mid2 = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.035),
        "LeftUpperLeg":  (-10.0, -3.0, 2.0),
        "LeftLowerLeg":  (14.0, 0.0, 0.0),
        "RightUpperLeg": (-18.0, 3.0, -2.0),
        "RightLowerLeg": (46.0, 0.0, 0.0),
    })
    animations["WalkBackward"] = {
        1: wb_a,
        13: wb_mid1,
        25: wb_b,
        37: wb_mid2,
        49: wb_a,
    }

    # 4. Jump (36 frames: prep -> airborne tuck -> land -> Idle)
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
    animations["Jump"] = {
        1: idle0,
        6: jump_prep,
        18: jump_peak,
        30: jump_prep,
        37: idle0,
    }

    # 5. Crouch (30 frames: Idle -> low guard -> Idle)
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
    animations["Crouch"] = {
        1: idle0,
        7: crouch_pose,
        24: crouch_pose,
        31: idle0,
    }

    # 6. BlockStand (24 frames: Idle -> tight high guard -> Idle)
    block_pose = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.065),
        "Spine":         (8.0, 0.0, 2.0),
        "Chest":         (8.0, 0.0, 2.0),
        "Neck":          (-8.0, 0.0, -12.0),
        "LeftShoulder":  (0.0, 16.0, -16.0),
        "LeftUpperArm":  (-12.0, 42.0, -68.0),
        "LeftLowerArm":  (18.0, -45.0, -112.0),
        "LeftHand":      (-18.0, 10.0, 15.0),
        "RightShoulder": (0.0, -16.0, 16.0),
        "RightUpperArm": (-12.0, -42.0, 68.0),
        "RightLowerArm": (18.0, 45.0, 112.0),
        "RightHand":     (-18.0, -10.0, -15.0),
    })
    animations["BlockStand"] = {
        1: idle0,
        5: block_pose,
        19: block_pose,
        25: idle0,
    }

    # 7. LightPunch (16 frames: Startup 4 [1..5], Active 3 [5..8], Recovery 9 [8..17])
    lp_ant = merge_pose(idle0, {
        "Hips":          (4.0, 0.0, 32.0),
        "Spine":         (5.0, 0.0, 8.0),
        "LeftShoulder":  (0.0, 10.0, -5.0),
        "LeftUpperArm":  (-5.0, 56.0, -42.0),
        "LeftLowerArm":  (10.0, -35.0, -108.0),
        "LeftHand":      (-12.0, -15.0, 10.0),
    })
    lp_hit = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.05),
        "Hips":          (4.0, 0.0, 6.0),
        "Spine":         (6.0, 0.0, -8.0),
        "Chest":         (6.0, 0.0, -12.0),
        "UpperChest":    (4.0, 0.0, -10.0),
        "Neck":          (-6.0, 0.0, 12.0),
        "Head":          (-6.0, 0.0, 12.0),
        # Lead left jab straight toward -Y; wrist & forearm pronation snap!
        "LeftShoulder":  (0.0, 8.0, -18.0),
        "LeftUpperArm":  (-8.0, 2.0, -62.0),
        "LeftLowerArm":  (0.0, -25.0, -4.0),
        "LeftHand":      (-18.0, -30.0, 0.0),
        # Rear hand stays tucked at chin
        "RightUpperArm": (-8.0, -58.0, 60.0),
        "RightLowerArm": (12.0, 36.0, 108.0),
    })
    animations["LightPunch"] = {
        1: idle0,
        3: lp_ant,
        5: lp_hit,
        8: lp_hit,
        17: idle0,
    }

    # 8. HeavyPunch (27 frames: Startup 7 [1..8], Active 4 [8..12], Recovery 16 [12..28])
    hp_ant = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.065),
        "Hips":          (6.0, 0.0, -8.0),
        "Spine":         (5.0, 0.0, -12.0),
        "Chest":         (4.0, 0.0, -15.0),
        "Neck":          (-5.0, 0.0, 18.0),
        "Head":          (-5.0, 0.0, 18.0),
        "RightShoulder": (0.0, -10.0, -12.0),
        "RightUpperArm": (-5.0, -45.0, 20.0),
        "RightLowerArm": (8.0, 25.0, 105.0),
        "RightHand":     (-15.0, 20.0, -10.0),
    })
    hp_hit = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.075),
        "Hips":          (8.0, 0.0, 14.0),
        "Spine":         (8.0, 0.0, 12.0),
        "Chest":         (8.0, 0.0, 14.0),
        "UpperChest":    (6.0, 0.0, 10.0),
        "Neck":          (-8.0, 0.0, -25.0),
        "Head":          (-8.0, 0.0, -25.0),
        # Rear right cross snaps straight forward (-Y) with forearm/wrist pronation
        "RightShoulder": (0.0, -6.0, 14.0),
        "RightUpperArm": (-10.0, 0.0, 32.0),
        "RightLowerArm": (0.0, 42.0, 4.0),
        "RightHand":     (-22.0, 40.0, 0.0),
        # Lead left hand pulls back to guard
        "LeftShoulder":  (0.0, 14.0, -10.0),
        "LeftUpperArm":  (-10.0, 58.0, -48.0),
        "LeftLowerArm":  (12.0, -35.0, -104.0),
        "LeftUpperLeg":  (-32.0, -4.0, 5.0),
        "LeftLowerLeg":  (38.0, 0.0, 0.0),
        "RightUpperLeg": (26.0, 6.0, -5.0),
        "RightLowerLeg": (22.0, 0.0, 0.0),
    })
    animations["HeavyPunch"] = {
        1: idle0,
        5: hp_ant,
        8: hp_hit,
        12: hp_hit,
        28: idle0,
    }

    # 9. LightKick (19 frames: Startup 5 [1..6], Active 3 [6..9], Recovery 11 [9..20])
    lk_ant = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.02),
        "Hips":          (-6.0, 0.0, 14.0),
        "LeftUpperLeg":  (-78.0, -4.0, 4.0),
        "LeftLowerLeg":  (95.0, 0.0, 0.0),
        "RightUpperLeg": (6.0, 4.0, -4.0),
        "RightLowerLeg": (12.0, 0.0, 0.0),
        "RightFoot":     (-12.0, 0.0, 0.0),
    })
    lk_hit = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.02),
        "Hips":          (-10.0, 0.0, 12.0),
        "Spine":         (-6.0, 0.0, 4.0),
        "Neck":          (8.0, 0.0, -10.0),
        "LeftUpperLeg":  (-68.0, -4.0, 4.0),
        "LeftLowerLeg":  (6.0, 0.0, 0.0),
        "LeftFoot":      (22.0, 0.0, 0.0),
        "RightUpperLeg": (6.0, 4.0, -4.0),
        "RightLowerLeg": (12.0, 0.0, 0.0),
        "RightFoot":     (-12.0, 0.0, 0.0),
    })
    animations["LightKick"] = {
        1: idle0,
        4: lk_ant,
        6: lk_hit,
        9: lk_hit,
        20: idle0,
    }

    # 10. HeavyKick (33 frames: Startup 9 [1..10], Active 4 [10..14], Recovery 20 [14..34])
    hk_ant = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.03),
        "Hips":          (-12.0, 6.0, -10.0),
        "Spine":         (-8.0, 0.0, 5.0),
        "Chest":         (4.0, 0.0, 6.0),
        "Neck":          (10.0, 0.0, 2.0),
        "LeftUpperLeg":  (-92.0, -8.0, 8.0),
        "LeftLowerLeg":  (105.0, 0.0, 0.0),
        "RightUpperLeg": (8.0, -5.0, 6.0),
        "RightLowerLeg": (12.0, 0.0, 0.0),
        "RightFoot":     (-10.0, 0.0, 0.0),
    })
    hk_hit = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.02),
        "Hips":          (-16.0, 8.0, -12.0),
        "Spine":         (-10.0, 0.0, 5.0),
        "Chest":         (5.0, 0.0, 8.0),
        "Neck":          (10.0, 0.0, 4.0),
        "Head":          (6.0, 0.0, 0.0),
        "LeftUpperLeg":  (-104.0, -8.0, 8.0),
        "LeftLowerLeg":  (6.0, 0.0, 0.0),
        "LeftFoot":      (25.0, 0.0, 0.0),
        "RightUpperLeg": (8.0, -6.0, 8.0),
        "RightLowerLeg": (12.0, 0.0, 0.0),
        "RightFoot":     (-10.0, 0.0, 0.0),
    })
    animations["HeavyKick"] = {
        1: idle0,
        6: hk_ant,
        10: hk_hit,
        14: hk_hit,
        34: idle0,
    }

    # 11. HitReact (20 frames: snap back -> recover to Idle)
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
    animations["HitReact"] = {
        1: idle0,
        5: hit_peak,
        9: hit_peak,
        21: idle0,
    }

    # 12. KnockDown (45 frames: Idle -> stagger -> fall onto back on ground)
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
    animations["KnockDown"] = {
        1: idle0,
        10: kd_stagger,
        24: kd_midfall,
        36: kd_ground,
        46: kd_ground,
    }

    # 13. GetUp (40 frames: Ground pose -> knee push -> Idle)
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
    animations["GetUp"] = {
        1: kd_ground,
        18: gu_push,
        32: crouch_pose,
        41: idle0,
    }

    # 14. Victory (60 frames: Idle -> raised fist celebration -> Idle)
    vic_pose = merge_pose(idle0, {
        "HipsOffset":    (0.0, 0.0, -0.01),
        "Hips":          (0.0, 0.0, 10.0),
        "Spine":         (-4.0, 0.0, 0.0),
        "Chest":         (-4.0, 0.0, 0.0),
        "Neck":          (-6.0, 0.0, -10.0),
        "Head":          (-4.0, 0.0, 0.0),
        "RightShoulder": (0.0, 18.0, 8.0),
        "RightUpperArm": (-20.0, 62.0, 35.0),
        "RightLowerArm": (0.0, 15.0, 68.0),
        "RightHand":     (0.0, 0.0, 0.0),
        "LeftUpperArm":  (-8.0, 62.0, -35.0),
        "LeftLowerArm":  (10.0, -25.0, -80.0),
        "LeftUpperLeg":  (-10.0, -4.0, 4.0),
        "LeftLowerLeg":  (12.0, 0.0, 0.0),
        "LeftFoot":      (-2.0, 0.0, 0.0),
        "RightUpperLeg": (8.0, 4.0, -4.0),
        "RightLowerLeg": (10.0, 0.0, 0.0),
        "RightFoot":     (-12.0, 0.0, 0.0),
    })
    animations["Victory"] = {
        1: idle0,
        16: vic_pose,
        44: vic_pose,
        61: idle0,
    }

    # 15. Defeat (50 frames: Idle -> slump to knees -> collapsed)
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
    animations["Defeat"] = {
        1: idle0,
        18: def_knees,
        36: def_knees,
        51: kd_ground,
    }

    for name, kf in animations.items():
        build_action_from_keyframes(arm_obj, name, kf)
        print(f"Generated action '{name}' (frames {min(kf.keys())}..{max(kf.keys())})")

    # Leave Idle active at frame 1
    arm_obj.animation_data.action = bpy.data.actions["Idle"]
    bpy.context.scene.frame_set(1)
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 61
    return list(animations.keys())


# ---------------------------------------------------------------------------
# Export FBX for Unity (Y Up, +Z Forward in Unity, Scale 1.0, 60 FPS)
# ---------------------------------------------------------------------------

def export_character_fbx(config):
    arm_obj = bpy.data.objects[config["armature_name"]]
    mesh_obj = bpy.data.objects[config["mesh_name"]]
    out_fbx = config["export_fbx"]
    os.makedirs(os.path.dirname(out_fbx), exist_ok=True)

    bpy.ops.object.mode_set(mode='OBJECT')
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


def run_animations_and_export(char_name="Lucas"):
    config = CHARACTER_CONFIGS[char_name]
    arm_obj = bpy.data.objects[config["armature_name"]]
    generate_all_animations(arm_obj, config)
    bpy.ops.wm.save_as_mainfile(filepath=config["work_blend"])
    export_character_fbx(config)
