import Metashape
import os


DISTANCE_M = 0.005
DISTANCE_LABEL = "5 mm"
SCALEBAR_LABEL = "Aerolithe scale bar 5 mm"
LOG_PATH = os.path.splitext(__file__)[0] + ".log" if "__file__" in globals() else None


def log(message):
    if LOG_PATH is None:
        return
    with open(LOG_PATH, "a", encoding="utf-8") as file:
        file.write(str(message) + "\n")


def current_chunk():
    doc = Metashape.app.document
    chunk = doc.chunk
    if chunk is None:
        raise RuntimeError("Aucun chunk actif trouve.")
    return chunk


def selected_markers(chunk):
    return [marker for marker in chunk.markers if getattr(marker, "selected", False)]


def selected_scalebars(chunk):
    return [scalebar for scalebar in chunk.scalebars if getattr(scalebar, "selected", False)]


def find_existing_scalebar(chunk, marker_a, marker_b):
    for scalebar in chunk.scalebars:
        same_order = scalebar.point0 == marker_a and scalebar.point1 == marker_b
        reverse_order = scalebar.point0 == marker_b and scalebar.point1 == marker_a
        if same_order or reverse_order:
            return scalebar
    return None


def choose_scalebar(chunk):
    markers = selected_markers(chunk)
    if len(markers) == 2:
        marker_a, marker_b = markers
        scalebar = find_existing_scalebar(chunk, marker_a, marker_b)
        if scalebar is None:
            scalebar = chunk.addScalebar(marker_a, marker_b)
            log("Nouvelle barre d'echelle creee entre les deux marqueurs selectionnes.")
        else:
            log("Barre d'echelle existante reutilisee pour les deux marqueurs selectionnes.")
        return scalebar

    if len(markers) > 0:
        raise RuntimeError(
            "Selectionne exactement deux marqueurs, pas "
            + str(len(markers))
            + "."
        )

    scalebars = selected_scalebars(chunk)
    if len(scalebars) == 1:
        log("Barre d'echelle selectionnee reutilisee.")
        return scalebars[0]

    if len(scalebars) > 1:
        raise RuntimeError("Selectionne une seule barre d'echelle, ou exactement deux marqueurs.")

    if len(chunk.scalebars) == 1:
        log("Aucune selection: l'unique barre d'echelle du chunk est reutilisee.")
        return chunk.scalebars[0]

    raise RuntimeError(
        "Selectionne exactement deux marqueurs, puis relance le script. "
        "Le script actuel ne choisit plus automatiquement chunk.scalebars[0]."
    )


def appliquer_echelle():
    if LOG_PATH is not None:
        with open(LOG_PATH, "w", encoding="utf-8") as file:
            file.write("Script Metashape Distance 5mm\n")

    chunk = current_chunk()
    scalebar = choose_scalebar(chunk)

    if scalebar.point0 is None or scalebar.point1 is None:
        raise RuntimeError("La barre d'echelle n'est pas liee a deux marqueurs valides.")

    scalebar.label = SCALEBAR_LABEL
    scalebar.reference.distance = DISTANCE_M

    log(
        "Distance de reference appliquee: "
        + str(DISTANCE_M)
        + " m ("
        + DISTANCE_LABEL
        + ")"
    )
    log("Marqueurs: " + scalebar.point0.label + " / " + scalebar.point1.label)

    try:
        chunk.updateTransform()
        log("Transformation du chunk mise a jour avec la barre d'echelle.")
    except Exception as exc:
        log("Barre d'echelle creee, mais updateTransform a echoue: " + str(exc))
        log("Dans Metashape, essaie Update Transform apres avoir verifie la barre d'echelle.")


try:
    appliquer_echelle()
except Exception as exc:
    log("Erreur: " + str(exc))
