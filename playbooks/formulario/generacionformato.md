# Playbook: formulario · FRM - CARP - Generacion de formato - Principal

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para formulario). El formato sale de la Identidad común y de lo que lee `herramientas/construir/formulario.py` (formato ampliado de `herramientas/README.md`). Componente agregado el 2026-11-03 por un pedido del usuario en la prueba guiada (fila 12.28).

## 1. Identidad

```json
{
  "tipo_playbook": "formulario",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.28",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0, verificados contra Dev el 2026-10-09. El diseño está en `diseno/07-diseno-app-y-flujos.md` §4.8 (2026-11-03). Ninguna columna de Generación de formato la edita una persona: las escribe el sistema al generar el formato (D-26), así que todo el formulario es de solo lectura.

Advertencias: etiquetas en los dos LCID, en español y sin tildes (BP-PP-197). `formulario.py` actualiza el Main existente.

## 2. Qué se crea

```json
{
  "tipo": "formulario",
  "nombre": "FRM - CARP - Generacion de formato - Principal",
  "tabla": "sanic_carp_tbl_generacionformato",
  "descripcion": "Formulario principal de Generación de formato, de solo lectura: a qué arreglo pertenece, con qué versión de la plantilla, quién lo generó y cuándo. La bitácora la escribe el sistema.",
  "solo_lectura": false,
  "pestana": {
    "1033": "General",
    "3082": "General"
  },
  "encabezado": [
    {"nombre": "ownerid", "solo_lectura": true}
  ],
  "secciones": [
    {
      "titulo": {
        "1033": "Generacion",
        "3082": "Generacion"
      },
      "campos": [
        {"nombre": "sanic_nombre", "solo_lectura": true},
        {"nombre": "sanic_arregloid", "solo_lectura": true},
        {"nombre": "sanic_versionplantillaid", "solo_lectura": true},
        {"nombre": "sanic_generadoporid", "solo_lectura": true},
        {"nombre": "sanic_fechageneracion", "solo_lectura": true}
      ]
    }
  ]
}
```

Decisiones y huecos:

- Hasta ahora la tabla tenía el Main de fábrica, que solo mostraba el número (`sanic_nombre`) y el propietario. Este formulario agrega el arreglo, la versión de plantilla, quién generó y la fecha, todos en solo lectura. El propietario se conserva en el encabezado, también de solo lectura.
- Sin JavaScript: no lleva bibliotecas ni manejadores. Se abre al hacer clic en una fila de la subgrilla "Formatos generados" del Arreglo (comportamiento nativo).
- `solo_lectura` de cada campo es la marca del formulario; la barrera real es el rol: ningún rol humano tiene Create ni Write sobre esta tabla (`diseno/04` §2), y el sistema escribe con S-07 y la Custom API de generación.
- El archivo generado nunca se guarda (RF-08): el formulario no tiene archivo ni descarga. Para volver a obtener el formato se pulsa **Generar formato** en el arreglo.
- Los tres lookups (`sanic_arregloid`, `sanic_versionplantillaid`, `sanic_generadoporid`) nacen con sus relaciones REL-06, REL-07 y REL-18; tienen que existir antes.

## 3. Precondiciones

| # | Qué tiene que existir | Cómo se comprueba |
|---|---|---|
| 1 | La tabla, la primaria, `sanic_fechageneracion` y los tres lookups, con etiqueta en 1033 y 3082 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_generacionformato')/Attributes` |
| 2 | La tabla figura en la solución con `rootcomponentbehavior` 0 | `GET solutioncomponents` |

## 4. Cómo se construye

```
python3 herramientas/construir/formulario.py playbooks/formulario/generacionformato.md
```

Orden: después de las relaciones 4.x. `formulario.py` actualiza el Main existente de la tabla y publica.

## 5. Verificación

```
python3 herramientas/construir/formulario.py playbooks/formulario/generacionformato.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET systemforms?$filter=objecttypecode eq 'sanic_carp_tbl_generacionformato' and type eq 2` | un solo Main · `name` y `description` los de la sección 2 · activo · no managed |
| 2 | `formxml` | igual al generado, etiquetas en los dos idiomas |
| 3 | Estructura | 1 pestaña, 1 sección, 5 campos, 0 subgrillas, 1 en el encabezado, 0 bibliotecas, 0 manejadores |
| 4 | Pertenencia | la tabla figura en la solución con `rootcomponentbehavior` 0 |
| 5 | Segunda ejecución | `ya_existia` |

Fuera de la herramienta, en Dev con un no administrador: abrir una fila de "Formatos generados" desde un arreglo muestra los cinco datos y ninguno se puede editar.

## 6. Si ya existe

Mismas comprobaciones que la sección 5. Si pasan: `ya_existia`; si no: `difiere` (corrige con `PATCH` y publica; un Main managed no se toca).

## 7. Reversa

Se restituye el `formxml` anterior desde la solución exportada. Reversible mientras no llegue a Test o Prod.

## 8. Fuera de alcance

La vista "Formatos generados del arreglo" (`playbooks/vista/formatos_generados_del_arreglo.md`) y la subgrilla en el Arreglo (`playbooks/formulario/arreglo.md`).
