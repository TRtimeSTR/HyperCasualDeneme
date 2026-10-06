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

    # Öksüz veri bloklarını temizle
    for mesh in bpy.data.meshes:
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for mat in bpy.data.materials:
        if mat.users == 0:
            bpy.data.materials.remove(mat)

def apply_transforms_and_origin():
    """Aktif objenin scale ve rotasyonunu dondurur (Apply All Transforms)."""
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

def apply_shading(obj, auto_smooth_angle=35):
    """Blender sürümüne uygun şekilde düzgün low-poly yumuşatma veya flat shading uygular."""
    try:
        # Blender 4.1+ 'Smooth by Angle' modifier
        bpy.ops.object.modifier_add(type='SMOOTH_BY_ANGLE')
        mod = obj.modifiers[-1]
        mod.angle = math.radians(auto_smooth_angle)
    except Exception:
        try:
            # Eski Blender sürümleri için klasik auto smooth
            obj.data.use_auto_smooth = True
            obj.data.auto_smooth_angle = math.radians(auto_smooth_angle)
        except Exception:
            bpy.ops.object.shade_flat()

def create_spike(name, width, height):
    """Hypercasual engel: Low-poly konik / piramit diken."""
    radius = max(0.1, width / 2.0)
    
    # 1. Ana Diken (6 kenarlı low-poly koni)
    bpy.ops.mesh.primitive_cone_add(
        vertices=6,
        radius1=radius,
        radius2=0.0,
        depth=height,
        location=(0, 0, height / 2.0)
    )
    cone_obj = bpy.context.active_object
    cone_obj.name = f"{name}_Cone"

    # 2. Taban Kaidesi (Alt silindir plinth)
    base_height = height * 0.15
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=6,
        radius=radius * 1.15,
        depth=base_height,
        location=(0, 0, base_height / 2.0)
    )
    base_obj = bpy.context.active_object
    base_obj.name = f"{name}_Base"

    # Birleştir
    cone_obj.select_set(True)
    base_obj.select_set(True)
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

    # Sol Sütun
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(-half_w, 0, height / 2.0)
    )
    left_pillar = bpy.context.active_object
    left_pillar.scale = (pillar_thickness, pillar_thickness, height)
    parts.append(left_pillar)

    # Sağ Sütun
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(half_w, 0, height / 2.0)
    )
    right_pillar = bpy.context.active_object
    right_pillar.scale = (pillar_thickness, pillar_thickness, height)
    parts.append(right_pillar)

    # Üst Kemer / Kiriş
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(0, 0, height - (pillar_thickness * 0.4))
    )
    top_beam = bpy.context.active_object
    top_beam.scale = (width + pillar_thickness, pillar_thickness * 1.2, pillar_thickness * 0.8)
    parts.append(top_beam)

    # Sol ve Sağ Taban Kaideleri (Plinths)
    for sign, p_name in [(-1, "Left"), (1, "Right")]:
        bpy.ops.mesh.primitive_cube_add(
            size=1.0,
            location=(sign * half_w, 0, 0.1)
        )
        base = bpy.context.active_object
        base.scale = (pillar_thickness * 1.6, pillar_thickness * 1.6, 0.2)
        parts.append(base)

    # Objeleri birleştir
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    final_obj = bpy.context.active_object
    final_obj.name = name
    apply_transforms_and_origin()
    apply_shading(final_obj, 30)
    return final_obj

def create_coin(name, width, height):
    """Hypercasual toplanabilir altın: Dik duran, pahlı silindir madeni para."""
    radius = max(0.2, width / 2.0)
    thickness = max(0.1, height * 0.25)
    
    # Dik durması için Y ekseninde 90 derece döndürülmüş 16 kenarlı silindir
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=16,
        radius=radius,
        depth=thickness,
        rotation=(math.radians(90), 0, 0),
        location=(0, 0, radius)
    )
    coin_obj = bpy.context.active_object
    coin_obj.name = name

    # Basit pah (Bevel) efekti ekle
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
    # '--' sonrası argümanları ayrıştır
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = []

    parser = argparse.ArgumentParser(description="Blender Procedural Asset Generator for Unity")
    parser.add_argument("--type", choices=["Spike", "MathGate", "Coin"], default="Spike", help="Model tipi")
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
    else:
        raise ValueError(f"Bilinmeyen model tipi: {args.type}")

    export_fbx(args.output, obj)

if __name__ == "__main__":
    main()
