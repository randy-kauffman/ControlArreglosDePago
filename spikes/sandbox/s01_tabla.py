"""Paso 1: tabla spk_versionplantilla (File + huella), filas de prueba y relectura del archivo (idempotente)."""
from sbx_lib import *

dv = Dataverse()
est, e, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{TABLA}')?$select=LogicalName,MetadataId,EntitySetName")
if est == 404:
    cuerpo = {"@odata.type": T + "EntityMetadata", "SchemaName": TABLA, "DisplayName": etiqueta("Version de plantilla spike"),
              "DisplayCollectionName": etiqueta("Versiones de plantilla spike"), "Description": etiqueta("Spike 1b - descartable"),
              "OwnershipType": "UserOwned", "HasActivities": False, "HasNotes": False, "IsActivity": False,
              "PrimaryNameAttribute": "spk_nombre",
              "Attributes": [dict(col_texto("spk_Nombre", "Nombre", 100), IsPrimaryName=True),
                             col_texto("spk_Huella", "Huella SHA-256", 64)]}
    est, b, _ = escribir(dv, "POST", "EntityDefinitions", cuerpo)
    mostrar("POST tabla", est, b)
    assert est in (200, 201, 204), "no se creo la tabla"
est, e, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{TABLA}')?$select=LogicalName,MetadataId,EntitySetName,OwnershipType")
mostrar("RELEER tabla", est, e)
assert e["EntitySetName"] == CONJUNTO, e

est, a, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{TABLA}')/Attributes(LogicalName='spk_archivo')?$select=LogicalName")
if est == 404:
    col = {"@odata.type": T + "FileAttributeMetadata", "SchemaName": "spk_Archivo", "DisplayName": etiqueta("Archivo de plantilla"),
           "Description": etiqueta("Plantilla .xlsx"), "RequiredLevel": REQ_NONE, "MaxSizeInKB": 5120}
    est, b, _ = escribir(dv, "POST", f"EntityDefinitions(LogicalName='{TABLA}')/Attributes", col)
    mostrar("POST columna File", est, b)
est, a, _ = dv.call("GET", f"EntityDefinitions(LogicalName='{TABLA}')/Attributes(LogicalName='spk_archivo')/Microsoft.Dynamics.CRM.FileAttributeMetadata?$select=LogicalName,MaxSizeInKB,AttributeTypeName")
mostrar("RELEER columna File", est, a)
assert est == 200 and a["MaxSizeInKB"] == 5120

with open(PLANTILLA, "rb") as f:
    xlsx = f.read()
h_ok = sha256(xlsx)
texto = b"esto no es un xlsx, solo texto plano\n"
filas = [
    ("v07 borrador (huella correcta)", h_ok, xlsx, "F-NIC-0000052_v07_borrador.xlsx"),
    ("v07 con huella incorrecta", ("0" if h_ok[0] != "0" else "1") + h_ok[1:], xlsx, "F-NIC-0000052_v07_borrador.xlsx"),
    ("archivo que no es xlsx (huella correcta)", sha256(texto), texto, "no_es_xlsx.xlsx"),
    ("sin archivo", h_ok, None, None),
]
estado = leer_estado()
estado.setdefault("filas", {})
for nombre, huella, datos, nombre_arch in filas:
    est, b, _ = dv.call("GET", f"{CONJUNTO}?$select=spk_versionplantillaid&$filter=spk_nombre eq '{nombre}'")
    if b.get("value"):
        rid = b["value"][0]["spk_versionplantillaid"]
        print("ya existia:", nombre, rid)
        dv.call("PATCH", f"{CONJUNTO}({rid})", {"spk_huella": huella})
    else:
        est, b, h = dv.call("POST", CONJUNTO, {"spk_nombre": nombre, "spk_huella": huella}, solucion=SOLUCION)
        assert est in (201, 204), (est, b)
        rid = dv.id_creado(h)
        print("creada:", nombre, rid)
    estado["filas"][nombre] = rid
    if datos is not None:
        est, _, _ = raw(dv, "PATCH", f"{CONJUNTO}({rid})/spk_archivo", datos,
                        {"Content-Type": "application/octet-stream", "x-ms-file-name": nombre_arch})
        print("  subida del archivo:", est)
        assert est in (200, 204)
guardar_estado(estado)

# Relectura por contenido: el archivo guardado debe tener la huella declarada, y el nombre de archivo y el tamano deben coincidir
for nombre, huella, datos, nombre_arch in filas:
    rid = estado["filas"][nombre]
    est, b, _ = dv.call("GET", f"{CONJUNTO}({rid})?$select=spk_nombre,spk_huella,spk_archivo,spk_archivo_name")
    assert b["spk_huella"] == huella, ("huella", nombre)
    if datos is None:
        assert not b.get("spk_archivo"), "no deberia tener archivo"
        print("OK", nombre, "sin archivo")
        continue
    est, cont, h = raw(dv, "GET", f"{CONJUNTO}({rid})/spk_archivo/$value")
    assert est == 200 and cont == datos, (nombre, est, len(cont))
    print("OK", nombre, "| archivo", b["spk_archivo_name"], len(cont), "bytes | sha256 releido", sha256(cont)[:16], "| huella guardada", b["spk_huella"][:16])
