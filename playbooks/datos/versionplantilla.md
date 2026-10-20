# Playbook: datos · Version de plantilla v07

> Recarga controlada de la plantilla (OA-4, 2026-10-10). Es un DATO, no un componente de solución: no viaja con la solución y se carga en cada entorno con este playbook y `mapeocelda.md`. Va en ese orden: el mapeo apunta a la versión por su número.

## 1. Identidad

```json
{
  "tipo_playbook": "datos",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "9.1",
  "fase": 1,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_carp_sol_controlarreglospago",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "carp",
  "prefijo_opciones": 15946,
  "idiomas": [
    1033,
    3082
  ]
}
```

De dónde sale cada dato: `diseno/01-convenciones.md` §0. La fila figura en `diseno/06-inventario-componentes.md` §9, renglón 9.1; la tabla en `diseno/02-diccionario-datos.md` §4.5.

## 2. Qué se crea

```json
{
  "tipo": "datos",
  "tabla": "sanic_carp_tbl_versionplantilla",
  "clave": [
    "sanic_nombre"
  ],
  "filas": [
    {
      "sanic_nombre": "v07",
      "sanic_capacidad": 60,
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      },
      "sanic_archivo": {
        "archivo": "../../spikes/plantilla/F-NIC-0000052_v07_borrador.xlsx",
        "nombre": "F-NIC-0000052_v07.xlsx"
      }
    }
  ]
}
```

- `sanic_nombre` = `v07`: la versión aprobada por Héctor Romero el 2026-10-09 (confirmado por el usuario el 2026-10-20: el archivo de `spikes/plantilla/F-NIC-0000052_v07_borrador.xlsx` es el aprobado). La clave de negocio es el número (K-03).
- `sanic_archivo`: se sube con el nombre `F-NIC-0000052_v07.xlsx` (el del mockup), no con el del archivo del spike. `datos.py` lo sube DESPUÉS del alta y lo verifica bajándolo y comparando la SHA-256.
- `sanic_capacidad` = 60: el bloque de cuotas de la hoja CONTROL DE PAGOS va de la fila 7 a la 66 (los totales están en la 67).
- `sanic_estado` NO se declara: nace en Borrador por defecto, y la publicación la hace el jefe con el gesto normal (Publicar). Si se declarara, la verificación diría `difiere` en cuanto la versión se publique.
- `ownerid`: el team por defecto de la BU `Arreglos de Pago`, como el resto de las filas del área.

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide | los de la sección 1 |
| 2 | Las tablas, los choices y el team propietario existen | `datos.py` los resuelve TODOS antes de escribir la primera fila; si falta uno, `bloqueado` sin tocar nada |
| 3 | No hay otra versión `v07` | la clave K-03 lo garantiza; si ya existe se verifica y no se duplica |

## 4. Cómo se construye

```
python3 herramientas/construir/datos.py playbooks/datos/versionplantilla.md
python3 herramientas/construir/datos.py playbooks/datos/mapeocelda.md
```

Después, el jefe (o el administrador en Dev) publica la versión con el gesto **Publicar**. Eso valida el mapeo contra las hojas reales del archivo (CARP-VER-005 a 009), calcula la huella y pasa la Vigente anterior a Histórica. En Prod se compara la huella con la de Test (OA-4).

## 5. Verificación

```
python3 herramientas/construir/datos.py playbooks/datos/versionplantilla.md --solo-verificar
```

`ya_existia` = la fila está, con el mismo archivo byte por byte. Una vez publicada, el archivo ya no se puede reemplazar (CARP-VER-003): `--completar` sobre una Vigente devuelve `error` con ese código, y está bien que así sea.
