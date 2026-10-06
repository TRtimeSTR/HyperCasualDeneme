import bpy
import sys
import argparse
import os
import math

def clean_scene():
    """Varsayılan Blender sahnesindeki tüm objeleri temizler."""
    if bpy.ops.object.mode_set.poll():
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

    for mesh in bpy.data.meshes:
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for mat in bpy.data.materials:
        if mat.users == 0:
            bpy.data.materials.remove(mat)

def create_principled_material(name, color_rgba, metallic=0.0, roughness=0.5):
    """Principled BSDF tabanlı renkli URP uyumlu materyal oluşturur."""
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        # Base Color
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = color_rgba
        # Metallic & Roughness
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
    return mat

def apply_transforms_and_origin():
    """Aktif objenin scale ve rotasyonunu dondurur (Apply All Transforms)."""
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

def apply_shading(obj, auto_smooth_angle=35):
    """Blender sürümüne uygun şekilde düzgün low-poly yumuşatma veya flat shading uygular."""
    try:
        bpy.ops.object.modifier_add(type='SMOOTH_BY_ANGLE')
        mod = obj.modifiers[-1]
        mod.angle = math.radians(auto_smooth_angle)
    except Exception:
        try:
            obj.data.use_auto_smooth = True
            obj.data.auto_smooth_angle = math.radians(auto_smooth_angle)
        except Exception:
            bpy.ops.object.shade_flat()

def create_spike(name, width, height):
    """Hypercasual engel: Kırmızı tehlike konisi ve koyu metalik kaide."""
    radius = max(0.1, width / 2.0)
    base_height = height * 0.18
    cone_height = height - base_height

    # 1. Taban Kaidesi (Koyu metalik taban)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8,
        radius=radius * 1.2,
        depth=base_height,
        location=(0, 0, base_height / 2.0)
    )
    base_obj = bpy.context.active_object
    base_obj.name = f"{name}_Base"
    mat_base = create_principled_material("Mat_SpikeBase", (0.15, 0.15, 0.18, 1.0), metallic=0.7, roughness=0.4)
    base_obj.data.materials.append(mat_base)

    # 2. Ana Diken (Canlı Kırmızı Tehlike Konisi)
    bpy.ops.mesh.primitive_cone_add(
        vertices=6,
        radius1=radius,
        radius2=0.0,
        depth=cone_height,
        location=(0, 0, base_height + (cone_height / 2.0))
    )
    cone_obj = bpy.context.active_object
    cone_obj.name = f"{name}_Cone"
    mat_spike = create_principled_material("Mat_SpikeRed", (0.95, 0.12, 0.12, 1.0), metallic=0.25, roughness=0.3)
    cone_obj.data.materials.append(mat_spike)

    # Birleştir
    base_obj.select_set(True)
    cone_obj.select_set(True)
    bpy.context.view_layer.objects.active = cone_obj
    bpy.ops.object.join()

    final_obj = bpy.context.active_object
    final_obj.name = name
    apply_transforms_and_origin()
    apply_shading(final_obj, 45)
    return final_obj

def create_math_gate(name, width, height):
    """Hypercasual kapı: İki sütun ve üst kemerden oluşan portal kemeri."""
    pillar_thickness = max(0.2, width * 0.12)
    half_w = width / 2.0
    parts = []

    mat_frame = create_principled_material("Mat_GateFrame", (0.18, 0.22, 0.32, 1.0), metallic=0.4, roughness=0.3)

    # Sol Sütun
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(-half_w, 0, height / 2.0)
    )
    left_pillar = bpy.context.active_object
    left_pillar.scale = (pillar_thickness, pillar_thickness, height)
    left_pillar.data.materials.append(mat_frame)
    parts.append(left_pillar)

    # Sağ Sütun
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(half_w, 0, height / 2.0)
    )
    right_pillar = bpy.context.active_object
    right_pillar.scale = (pillar_thickness, pillar_thickness, height)
    right_pillar.data.materials.append(mat_frame)
    parts.append(right_pillar)

    # Üst Kemer / Kiriş
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(0, 0, height - (pillar_thickness * 0.4))
    )
    top_beam = bpy.context.active_object
    top_beam.scale = (width + pillar_thickness, pillar_thickness * 1.2, pillar_thickness * 0.8)
    top_beam.data.materials.append(mat_frame)
    parts.append(top_beam)

    # Sol ve Sağ Kaideler
    for sign in [-1, 1]:
        bpy.ops.mesh.primitive_cube_add(
            size=1.0,
            location=(sign * half_w, 0, 0.1)
        )
        base = bpy.context.active_object
        base.scale = (pillar_thickness * 1.6, pillar_thickness * 1.6, 0.2)
        base.data.materials.append(mat_frame)
        parts.append(base)

    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    final_obj = bpy.context.active_object
    final_obj.name = name
    apply_transforms_and_origin()
    apply_shading(final_obj, 35)
    return final_obj

def create_coin(name, width, height):
    """Hypercasual toplanabilir altın: Dik duran, parlak sarı madeni para."""
    radius = max(0.2, width / 2.0)
    thickness = max(0.1, height * 0.25)

    bpy.ops.mesh.primitive_cylinder_add(
        vertices=16,
        radius=radius,
        depth=thickness,
        rotation=(math.radians(90), 0, 0),
        location=(0, 0, radius)
    )
    coin_obj = bpy.context.active_object
    coin_obj.name = name

    mat_gold = create_principled_material("Mat_CoinGold", (1.0, 0.82, 0.1, 1.0), metallic=0.85, roughness=0.2)
    coin_obj.data.materials.append(mat_gold)

    try:
        bpy.ops.object.modifier_add(type='BEVEL')
        bev = coin_obj.modifiers[-1]
        bev.width = radius * 0.08
        bev.segments = 2
    except Exception:
        pass

    apply_transforms_and_origin()
    apply_shading(coin_obj, 40)
    return coin_obj

def create_finish_arch(name, width, height):
    """Hypercasual Bitiş Kemeri (Finish Arch): Altın ve damalı zafer kapısı."""
    half_w = width / 2.0
    pillar_r = max(0.25, width * 0.08)
    parts = []

    mat_arch_gold = create_principled_material("Mat_FinishGold", (0.95, 0.75, 0.15, 1.0), metallic=0.7, roughness=0.3)
    mat_banner = create_principled_material("Mat_FinishBanner", (0.1, 0.1, 0.1, 1.0), metallic=0.1, roughness=0.6)

    # Sol Sütun (8 kenarlı silindir)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8,
        radius=pillar_r,
        depth=height,
        location=(-half_w, 0, height / 2.0)
    )
    left_p = bpy.context.active_object
    left_p.data.materials.append(mat_arch_gold)
    parts.append(left_p)

    # Sağ Sütun
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8,
        radius=pillar_r,
        depth=height,
        location=(half_w, 0, height / 2.0)
    )
    right_p = bpy.context.active_object
    right_p.data.materials.append(mat_arch_gold)
    parts.append(right_p)

    # Üst Kemer Kirişi
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(0, 0, height + 0.3)
    )
    beam = bpy.context.active_object
    beam.scale = (width + (pillar_r * 2.5), pillar_r * 2.0, 0.6)
    beam.data.materials.append(mat_arch_gold)
    parts.append(beam)

    # Zafer Paneli (Banner)
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(0, 0, height - 0.2)
    )
    banner = bpy.context.active_object
    banner.scale = (width * 0.8, 0.1, 0.8)
    banner.data.materials.append(mat_banner)
    parts.append(banner)

    # Birleştir
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    final_obj = bpy.context.active_object
    final_obj.name = name
    apply_transforms_and_origin()
    apply_shading(final_obj, 35)
    return final_obj

def export_fbx(output_path, obj):
    """Unity uyumlu FBX export eder (0,0,0 rotasyon ve düzgün eksen oryantasyonu)."""
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    bpy.ops.export_scene.fbx(
        filepath=output_path,
        check_existing=False,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
        object_types={'MESH'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        axis_forward='-Z',
        axis_up='Y'
    )
    print(f"[SUCCESS] Model başarıyla export edildi: {output_path}")

def main():
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = []

    parser = argparse.ArgumentParser(description="Blender Procedural Asset Generator for Unity")
    parser.add_argument("--type", choices=["Spike", "MathGate", "Coin", "FinishArch"], default="Spike", help="Model tipi")
    parser.add_argument("--width", type=float, default=1.5, help="Model genişliği")
    parser.add_argument("--height", type=float, default=2.0, help="Model yüksekliği")
    parser.add_argument("--name", type=str, default="GeneratedAsset", help="Obje adı")
    parser.add_argument("--output", type=str, required=True, help="Çıktı FBX dosya yolu")

    args = parser.parse_args(argv)

    print(f"[INFO] Mesh üretimi başlıyor: Type={args.type}, Name={args.name}, Width={args.width}, Height={args.height}")
    clean_scene()

    if args.type == "Spike":
        obj = create_spike(args.name, args.width, args.height)
    elif args.type == "MathGate":
        obj = create_math_gate(args.name, args.width, args.height)
    elif args.type == "Coin":
        obj = create_coin(args.name, args.width, args.height)
    elif args.type == "FinishArch":
        obj = create_finish_arch(args.name, args.width, args.height)
    else:
        raise ValueError(f"Bilinmeyen model tipi: {args.type}")

    export_fbx(args.output, obj)

if __name__ == "__main__":
    main()
