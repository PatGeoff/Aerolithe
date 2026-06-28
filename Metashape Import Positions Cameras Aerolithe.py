import csv
import os

import Metashape


IMAGE_EXTENSIONS = {
    ".jpg",
    ".jpeg",
    ".png",
    ".tif",
    ".tiff",
}

D850_SENSOR_WIDTH_MM = 35.9
SIGMA_MACRO_FOCAL_MM = 105.0

def show_message(message):
    try:
        Metashape.app.messageBox(message)
    except Exception:
        print(message)


def find_images_root_from_path(path):
    current = path
    if os.path.isfile(current):
        current = os.path.dirname(current)

    while current:
        name = os.path.basename(current)
        if name in ("images", "_images"):
            return current

        parent = os.path.dirname(current)
        if parent == current:
            break
        current = parent

    return ""


def get_images_root_from_qt():
    for binding in ("PySide6", "PySide2"):
        try:
            widgets = __import__(binding + ".QtWidgets", fromlist=["QtWidgets"])
            dialog = widgets.QFileDialog
            folder = dialog.getExistingDirectory(
                None,
                "Choisir le dossier images ou _images Aerolithe",
                "",
                dialog.Option.ShowDirsOnly,
            )
            return folder or ""
        except Exception:
            continue

    return ""


def get_images_root_fallback():
    selected_image = Metashape.app.getOpenFileName(
        "Fallback: choisir une image dans images/_images, ou Annuler pour seulement importer le CSV",
        "",
        "Images (*.jpg *.jpeg *.png *.tif *.tiff)",
    )

    if not selected_image:
        return ""

    images_root = find_images_root_from_path(selected_image)
    if not images_root:
        show_message(
            "Impossible de retrouver un dossier parent nomme images ou _images depuis l'image choisie."
        )

    return images_root


def get_images_root():
    show_message(
        "Optionnel: choisis le dossier images/_images Aerolithe pour ajouter les photos au chunk.\n"
        "Si tu annules, le script passera directement au choix du CSV."
    )

    images_root = get_images_root_from_qt()
    if images_root:
        return images_root

    return get_images_root_fallback()


def list_images(folder):
    if not os.path.isdir(folder):
        return []

    return [
        os.path.join(folder, name)
        for name in sorted(os.listdir(folder))
        if os.path.splitext(name)[1].lower() in IMAGE_EXTENSIONS
        and os.path.isfile(os.path.join(folder, name))
    ]


def get_existing_photo_paths(chunk):
    existing = set()

    for camera in chunk.cameras:
        if camera.photo:
            existing.add(os.path.abspath(camera.photo.path))

    return existing


def get_side_folder_specs(images_root, side):
    return [
        (
            "focusStack_" + side,
            os.path.join(images_root, "focusStack", "focusStack_" + side),
        ),
        (
            "noFS/serie_" + side,
            os.path.join(images_root, "noFS", "serie_" + side),
        ),
        (
            "serie_" + side,
            os.path.join(images_root, "mesures", "serie_" + side),
        ),
    ]


def collect_side_images(images_root, side):
    image_paths = []

    for label, folder in get_side_folder_specs(images_root, side):
        paths = list_images(folder)
        if paths:
            print("Dossier trouve:", label, len(paths), folder)
            image_paths.extend(paths)
        else:
            print("Dossier ignore, vide ou absent:", label, folder)

    return image_paths


def add_images_to_chunk(chunk, image_paths, side):
    existing_paths = get_existing_photo_paths(chunk)
    new_paths = [
        path for path in image_paths
        if os.path.abspath(path) not in existing_paths
    ]

    if not new_paths:
        print("Aucune nouvelle photo a ajouter au chunk cote", side)
        return 0

    chunk.addPhotos(new_paths)
    print("Photos ajoutees au chunk cote", side + ":", len(new_paths))
    return len(new_paths)


def force_single_sensor(chunk):
    cameras = [camera for camera in chunk.cameras if camera.photo]

    if not cameras:
        print("Sensor unique ignore: aucune camera dans chunk", chunk.label)
        return

    base_sensor = cameras[0].sensor
    changed = 0

    for camera in cameras:
        if camera.sensor != base_sensor:
            camera.sensor = base_sensor
            changed += 1

    reset_sensor_calibration(base_sensor)

    print(
        "Sensor unique applique pour chunk:",
        chunk.label,
        "cameras:",
        len(cameras),
        "modifiees:",
        changed,
        "sensor:",
        base_sensor.label,
    )


def reset_sensor_calibration(sensor):
    reset_parts = []
    width = 0
    height = 0

    try:
        width = int(sensor.width)
        height = int(sensor.height)
    except Exception as ex:
        print("Lecture taille sensor ignoree:", ex)

    try:
        calib = Metashape.Calibration()
        if width > 0 and height > 0:
            calib.width = width
            calib.height = height
            calib.f = width * SIGMA_MACRO_FOCAL_MM / D850_SENSOR_WIDTH_MM
            calib.cx = 0
            calib.cy = 0

        for attr in ("k1", "k2", "k3", "k4", "p1", "p2", "b1", "b2"):
            try:
                setattr(calib, attr, 0)
            except Exception:
                pass

        sensor.user_calib = calib
        reset_parts.append("user_calib propre")
    except Exception as ex:
        print("Reset user_calib ignore:", ex)

    try:
        sensor.calibration = None
        reset_parts.append("calibration=None")
    except Exception as ex:
        print("Reset sensor.calibration ignore:", ex)

    try:
        sensor.fixed = False
        reset_parts.append("fixed=false")
    except Exception as ex:
        print("Reset sensor.fixed ignore:", ex)

    try:
        sensor.fixed_calibration = False
        reset_parts.append("fixed_calibration=false")
    except Exception as ex:
        print("Reset sensor.fixed_calibration ignore:", ex)

    try:
        sensor.fixed_params = []
        reset_parts.append("fixed_params=[]")
    except Exception as ex:
        print("Reset sensor.fixed_params ignore:", ex)

    print(
        "Calibration sensor remise a zero:",
        sensor.label,
        str(width) + "x" + str(height),
        ", ".join(reset_parts),
    )


def add_aerolithe_images_to_chunks(chunk_a):
    images_root = get_images_root()

    if not images_root:
        print("Import photos ignore: aucun dossier images choisi.")
        return [chunk_a]

    side_a_images = collect_side_images(images_root, "A")
    side_b_images = collect_side_images(images_root, "B")

    chunks = [chunk_a]
    added_a = add_images_to_chunk(chunk_a, side_a_images, "A")
    force_single_sensor(chunk_a)
    print("Chunk cote A:", chunk_a.label, "photos ajoutees:", added_a)

    if side_b_images:
        doc = Metashape.app.document
        chunk_b = doc.addChunk()
        chunk_b.label = "Aerolithe cote B"
        added_b = add_images_to_chunk(chunk_b, side_b_images, "B")
        force_single_sensor(chunk_b)
        chunks.append(chunk_b)
        print("Chunk cote B:", chunk_b.label, "photos ajoutees:", added_b)
    else:
        print("Aucun chunk B cree: aucun fichier dans focusStack_B ou serie_B.")

    return chunks


def get_camera_maps(chunk):
    cameras_by_filename = {}
    cameras_by_label = {}

    for camera in chunk.cameras:
        if camera.photo:
            cameras_by_filename[os.path.basename(camera.photo.path)] = camera
        cameras_by_label[camera.label] = camera

    return cameras_by_filename, cameras_by_label


def clear_chunk_alignment(chunk):
    cleared = 0

    for camera in chunk.cameras:
        if camera.transform is not None:
            camera.transform = None
            cleared += 1

    if cleared:
        print("Alignement courant efface pour chunk:", chunk.label, cleared)


def import_camera_references_for_chunk(csv_path, chunk):
    clear_chunk_alignment(chunk)
    cameras_by_filename, cameras_by_label = get_camera_maps(chunk)
    matched = 0
    ignored = 0

    with open(csv_path, newline="", encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)

        for row in reader:
            filename = row.get("FileName", "").strip()
            label = row.get("Label", "").strip()

            camera = cameras_by_filename.get(filename)
            if camera is None:
                camera = cameras_by_label.get(label)

            if camera is None:
                ignored += 1
                continue

            camera.reference.location = Metashape.Vector([
                float(row["X"]),
                float(row["Y"]),
                float(row["Z"]),
            ])
            camera.reference.location_accuracy = Metashape.Vector([0.25, 0.25, 0.25])
            camera.reference.enabled = True
            matched += 1

    chunk.updateTransform()

    print("Import positions cameras Aerolithe termine pour chunk:", chunk.label)
    print("CSV:", csv_path)
    print("Cameras associees:", matched)
    print("Lignes CSV ignorees pour ce chunk:", ignored)


def import_camera_references(csv_path, chunks):
    for chunk in chunks:
        import_camera_references_for_chunk(csv_path, chunk)


def get_active_chunk():
    doc = Metashape.app.document
    chunk = doc.chunk

    if chunk is None:
        raise Exception("Aucun chunk actif.")

    return chunk


chunk = get_active_chunk()
chunks = add_aerolithe_images_to_chunks(chunk)

csv_path = Metashape.app.getOpenFileName(
    "Choisir le CSV des positions cameras Aerolithe",
    "",
    "CSV (*.csv)",
)

if not csv_path:
    raise Exception("Aucun fichier CSV choisi.")

import_camera_references(csv_path, chunks)
