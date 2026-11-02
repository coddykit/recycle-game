"""Grill Convoy — Play store art rendered from the repo's own models (Meshy burger / chef / cashier / fan in
unity/Assets/Art/Meshy + the Blender kit in unity/Assets/Resources/kit): three app-icon candidates (512 × 512, full-bleed,
no text, readable at 48 px) and the feature graphic (1024 × 500, no text: the tagline from store/screenshots/en.json is
laid over it by the store-art template). EEVEE, headless. No real brands, no logos, no text in any frame.
  blender -b --python grill-convoy/tools/art/render_store.py -- <unity dir> <store dir>
Outputs: <store>/icon-candidates/c1.png (burger hero), c2.png (burger truck), c3.png (chef + burger), <store>/feature-graphic.png
The owner picks the icon later (docs/ART.md); the build keeps the placeholder in unity/Assets/Art/Icon until then.
"""
import math
import os
import sys

import bpy
import mathutils

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
UNITY = argv[0]
STORE = argv[1]
A = os.path.join(UNITY, "Assets")
ICONS = os.path.join(STORE, "icon-candidates")


def srgb(h):
    """'#RRGGBB' -> linear RGB (Blender colour inputs are linear)."""
    c = [int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def reset(world="#F6E3C0", strength=1.0):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    sc.eevee.use_gtao = True
    sc.eevee.gtao_distance = 1.5
    sc.eevee.taa_render_samples = 64
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (*srgb(world), 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = strength
    sc.world = w


def sun(energy=3.2, rot=(45, 8, -30), color=(1, 1, 1)):
    s = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    s.data.energy = energy
    s.data.color = color
    s.rotation_euler = mathutils.Euler([math.radians(a) for a in rot])
    bpy.context.scene.collection.objects.link(s)
    return s


def emission_mat(name, inner, outer, radial=True):
    """Unlit backdrop: radial (or vertical) gradient inner -> outer."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    grad = nt.nodes.new("ShaderNodeTexGradient")
    coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    grad.gradient_type = "SPHERICAL" if radial else "LINEAR"
    if radial:
        mapping.inputs["Location"].default_value = (-0.5, -0.5, 0)
        mapping.inputs["Scale"].default_value = (1.4, 1.4, 1)
        mapping.inputs["Location"].default_value = (-0.7, -0.7, 0)
    else:
        mapping.inputs["Rotation"].default_value = (0, 0, math.radians(90))
    nt.links.new(coord.outputs["UV"], mapping.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], grad.inputs["Vector"])
    nt.links.new(grad.outputs["Fac"], ramp.inputs["Fac"])
    ramp.color_ramp.elements[0].color = (*srgb(outer), 1)
    ramp.color_ramp.elements[1].color = (*srgb(inner), 1)
    nt.links.new(ramp.outputs["Color"], em.inputs["Color"])
    nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
    return m


def backdrop(cam, inner, outer, dist=30.0, size=60.0, radial=True):
    """A gradient card behind everything, facing the camera."""
    bpy.ops.mesh.primitive_plane_add(size=size)
    p = bpy.context.active_object
    p.data.materials.append(emission_mat("bg", inner, outer, radial))
    fwd = cam.matrix_world.to_quaternion() @ mathutils.Vector((0, 0, -1))
    p.location = cam.location + fwd * dist
    p.rotation_euler = cam.matrix_world.to_euler()
    return p


def textured(obj, png, interp="Linear"):
    m = bpy.data.materials.new(os.path.basename(png))
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    tex = m.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(png)
    tex.interpolation = interp
    m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.75
    obj.data.materials.clear()
    obj.data.materials.append(m)


def load(path, png, loc=(0, 0, 0), yaw=0.0, scale=1.0, interp="Linear"):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    root = new[0]
    for o in new:
        if o.parent is None:
            root = o
    for o in new:
        if o.type == "MESH":
            textured(o, png, interp)
    root.location = loc
    root.rotation_euler = (root.rotation_euler.x, root.rotation_euler.y, math.radians(yaw))
    root.scale = (root.scale.x * scale, root.scale.y * scale, root.scale.z * scale)
    return root


def char(name, loc, yaw=0.0, scale=1.0):
    return load(os.path.join(A, "Art/Meshy/%s_lod0.fbx" % name), os.path.join(A, "Art/Meshy/%s_base.png" % name), loc, yaw, scale)


def kit(name, loc, yaw=0.0, scale=1.0):
    return load(os.path.join(A, "Resources/kit/%s.fbx" % name), os.path.join(A, "Resources/materials/palette.png"), loc, yaw, scale, "Closest")


def ground(color, size=200, loc=(0, 0, 0)):
    bpy.ops.mesh.primitive_plane_add(size=size, location=loc)
    p = bpy.context.active_object
    m = bpy.data.materials.new("ground")
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*srgb(color), 1)
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    p.data.materials.append(m)
    return p


def camera(loc, look, lens=50, ortho=None):
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    bpy.context.scene.collection.objects.link(cam)
    cam.location = loc
    d = mathutils.Vector(look) - mathutils.Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.lens = lens
    cam.data.clip_end = 2000
    bpy.context.scene.camera = cam
    bpy.context.view_layer.update()
    return cam


def render(path, w, h):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDER", path)


def icon_burger():
    """c1: the burger hero, big and centred, on a hot sunset radial."""
    reset()
    sun(3.4, (40, 10, -35))
    sun(1.2, (70, 0, 150))
    char("burger", (0, 0, 0), yaw=-20, scale=3.4)
    cam = camera((0, -7.0, 4.2), (0, 0, 0.92), ortho=3.6)
    backdrop(cam, "#FFD36E", "#E8542F", dist=20, size=3.7)
    render(os.path.join(ICONS, "c1.png"), 512, 512)


def icon_truck():
    """c2: the burger truck in 3/4, striped awning and bun badge toward the viewer, teal sky, warm paving."""
    reset(world="#9FD3CF")
    sun(3.6, (48, 8, -40))
    sun(1.0, (70, 0, 140))
    kit("truck_starter", (0, 0, 0), yaw=-125)
    cam = camera((-6.2, -6.6, 3.6), (0.55, 0.35, 2.0), ortho=6.6)
    backdrop(cam, "#CFF0E8", "#2D8C8A", dist=40, size=7.4)
    render(os.path.join(ICONS, "c2.png"), 512, 512)


def icon_chef():
    """c3: the chef with a giant burger, cream-to-orange radial."""
    reset()
    sun(3.2, (45, 8, -25))
    sun(1.1, (70, 0, 150))
    char("chef", (-0.12, 0.3, 0), yaw=12)
    char("burger", (0.26, -0.8, 0.9), yaw=-25, scale=0.6)
    cam = camera((0.0, -6.0, 1.95), (0.0, 0.0, 1.45), ortho=1.15)
    backdrop(cam, "#FFF4DC", "#F08A5D", dist=20, size=1.2)
    render(os.path.join(ICONS, "c3.png"), 512, 512)


_mats = {}


def solid(hexcol):
    if hexcol not in _mats:
        m = bpy.data.materials.new("c" + hexcol)
        m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*srgb(hexcol), 1)
        m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
        _mats[hexcol] = m
    return _mats[hexcol]


def cube(center, size, hexcol, yaw=0.0, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    c = bpy.context.active_object
    c.scale = size
    c.data.materials.append(solid(hexcol))
    if parent is not None:
        c.parent = parent
    return c


def figure(x, y, yaw, shirt, burger=False, s=0.62):
    """The game's own chunky crowd figure (MeshKit.Figure): legs, shirt, head; a burger in hand when served."""
    root = bpy.data.objects.new("fig", None)
    bpy.context.scene.collection.objects.link(root)
    for lx in (-0.35, 0.35):
        cube((lx * s, 0, 0.55 * s), (0.5 * s, 0.6 * s, 1.1 * s), "#3B4A5A", parent=root)
    cube((0, 0, 1.65 * s), (1.4 * s, 0.8 * s, 1.1 * s), shirt, parent=root)
    cube((0, 0, 2.55 * s), (0.8 * s, 0.8 * s, 0.7 * s), "#E8B894", parent=root)
    if burger:
        cube((0.95 * s, -0.2 * s, 1.25 * s), (0.7 * s, 0.7 * s, 0.25 * s), "#E3A857", parent=root)
        cube((0.95 * s, -0.2 * s, 1.45 * s), (0.75 * s, 0.75 * s, 0.15 * s), "#6B3A26", parent=root)
        cube((0.95 * s, -0.2 * s, 1.62 * s), (0.7 * s, 0.7 * s, 0.25 * s), "#E3A857", parent=root)
    root.location = (x, y, 0)
    root.rotation_euler = (0, 0, math.radians(yaw))
    return root


def feature_graphic():
    """1024 × 500: lunch rush on the plaza — two trucks with striped awnings, the crew at the hatches, a line of customers
    (the game's own chunky crowd figures), string lights, planters, office towers and a warm sky. A big burger holds the
    left third (the store-art template lays the tagline next to it)."""
    reset(world="#FFD7A0", strength=0.8)
    sun(3.4, (52, 6, -50), color=(1.0, 0.93, 0.82))
    sun(0.9, (70, 0, 140))
    ground("#D8CDBB", size=120)
    ground("#9BC46A", size=600, loc=(0, 0, -0.03))
    kit("truck_starter", (2.0, 5.0, 0), yaw=90)
    kit("truck_box", (12.0, 5.6, 0), yaw=90)
    kit("menu_board", (6.6, 2.2, 0), yaw=-10)
    kit("d_lights", (7.0, 13.0, 0), yaw=0)
    kit("d_planter", (-7.0, 9.0, 0), yaw=0)
    kit("d_planter", (22.0, 9.0, 0), yaw=0)
    kit("d_bits", (18.0, 1.5, 0), yaw=10)
    kit("parasol", (-3.0, 12.0, 0))
    kit("parasol", (17.5, 12.5, 0))
    for i, (x, h) in enumerate([(-34, 1.0), (-14, 1.15), (6, 0.9), (26, 1.2), (46, 1.05)]):
        kit("d_office_a" if i % 2 == 0 else "d_office_b", (x, 60 + (i % 2) * 8, 0), yaw=0, scale=h)
    kit("d_house_a", (-22, 30, 0), yaw=0)
    kit("d_shop_a", (34, 30, 0), yaw=0)
    kit("d_kiosk", (-30, 24, 0), yaw=0, scale=0.9)
    char("cashier", (2.6, 2.2, 0), yaw=95, scale=1.0)
    char("chef", (12.4, 2.5, 0), yaw=85, scale=1.0)
    shirts = ["#4C7FD1", "#E57A3C", "#9B59B6", "#3FB0AC", "#F2C744", "#E57A3C", "#4C7FD1", "#3FB0AC", "#9B59B6"]
    line = [(4.6, 0.6), (5.2, -0.6), (6.0, -1.8), (7.0, -3.0), (13.6, 0.8), (14.4, -0.4), (15.4, -1.6), (16.6, -2.6), (17.8, -3.4)]
    for i, (x, y) in enumerate(line):
        figure(x, y, 160 + (i * 23) % 40, shirts[i])
    figure(-1.0, -1.0, -20, "#3FB0AC", burger=True)
    figure(9.6, -2.2, 200, "#F2C744", burger=True)
    char("burger", (-6.4, -5.5, 0.2), yaw=-25, scale=4.2)
    cam = camera((5.0, -22.0, 7.0), (6.0, 10.0, 3.2), lens=30)
    backdrop(cam, "#FFE7B8", "#F59A62", dist=300, size=700, radial=False)
    render(os.path.join(STORE, "feature-graphic.png"), 1024, 500)


def main():
    os.makedirs(ICONS, exist_ok=True)
    icon_burger()
    icon_truck()
    icon_chef()
    feature_graphic()
    print("RENDER_STORE_DONE")


main()
