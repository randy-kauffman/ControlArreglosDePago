# Playbook: importacion · comandos de la barra (12.24)

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para importación). El formato sale de la Identidad común y de lo que lee `herramientas/construir/importar_solucion.py` (formato ampliado de `herramientas/README.md`: botones en `Form` con `PrimaryControl`).

Los 13 botones de los gestos (lote B) van en **un solo playbook** porque un cambio `ribbon` reemplaza el `RibbonDiffXml` **entero** de su tabla: una entrada por tabla, con todos sus botones. El gesto 12 (Registrar pago) no tiene botón: es el Guardar nativo del formulario de Cuota.

## 1. Identidad

```json
{
  "tipo_playbook": "importacion",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.24",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0. El contenido de los botones es `diseno/07-diseno-app-y-flujos.md` §5.4 (gestos) y §5.6 (botones por tabla); las funciones están en `webresources/js/comandos.js` (playbook `playbooks/web-resource/sanic_carp_wr_js_comandos.md`).

## 2. Qué se crea

```json
{
  "tipo": "importacion",
  "app": "sanic_carp_mda_controlarreglospago",
  "tablas_excluidas_de_ocultar": [
    "sanic_carp_tbl_cliente",
    "sanic_carp_tbl_cuota",
    "sanic_carp_tbl_generacionformato",
    "sanic_carp_tbl_mapeocelda",
    "email"
  ],
  "cambios": [
    {
      "clase": "ribbon",
      "tabla": "sanic_carp_tbl_conversacion",
      "biblioteca": "sanic_carp_wr_js_comandos",
      "botones": [
        {
          "id": "tomar",
          "regla": "SanicCarp.Conversacion.puedeTomar",
          "funcion": "SanicCarp.Conversacion.tomar",
          "icono": "sanic_carp_wr_svg_accion",
          "etiqueta": {"1033": "Take", "3082": "Tomar"},
          "tooltip": {
            "1033": "Assigns the conversation to you.",
            "3082": "Le asigna la conversación a usted."
          },
          "ubicaciones": ["Form", "HomepageGrid"],
          "secuencia": 10
        },
        {
          "id": "finalizar",
          "regla": "SanicCarp.Conversacion.puedeFinalizar",
          "funcion": "SanicCarp.Conversacion.finalizar",
          "icono": "sanic_carp_wr_svg_accion",
          "etiqueta": {"1033": "Finish", "3082": "Finalizar"},
          "tooltip": {
            "1033": "Finishes the conversation with the result, reason and detail typed in the form.",
            "3082": "Finaliza la conversación con el resultado, el motivo y el detalle escritos en el formulario."
          },
          "ubicaciones": ["Form"],
          "secuencia": 20
        },
        {
          "id": "unir",
          "regla": "SanicCarp.Conversacion.puedeUnir",
          "funcion": "SanicCarp.Conversacion.unir",
          "icono": "sanic_carp_wr_svg_accion",
          "etiqueta": {"1033": "Merge into another conversation", "3082": "Unir a otra conversación"},
          "tooltip": {
            "1033": "Moves the emails of this conversation to another open one. It cannot be undone.",
            "3082": "Pasa los correos de esta conversación a otra abierta. No se puede deshacer."
          },
          "ubicaciones": ["Form"],
          "secuencia": 30
        },
        {
          "id": "reabrir",
          "regla": "SanicCarp.Conversacion.puedeReabrir",
          "funcion": "SanicCarp.Conversacion.reabrir",
          "icono": "sanic_carp_wr_svg_accion",
          "etiqueta": {"1033": "Reopen", "3082": "Reabrir"},
          "tooltip": {
            "1033": "Reopens the finished conversation and clears its result and finish data.",
            "3082": "Reabre la conversación finalizada y limpia su resultado y los datos de finalización."
          },
          "ubicaciones": ["Form"],
          "secuencia": 40
        },
        {
          "id": "iniciarnueva",
          "regla": "SanicCarp.Conversacion.puedeIniciarNueva",
          "funcion": "SanicCarp.Conversacion.iniciarNueva",
          "icono": "sanic_carp_wr_svg_accion",
          "etiqueta": {"1033": "Start new conversation", "3082": "Iniciar conversación nueva"},
          "tooltip": {
            "1033": "Moves the email that arrived after the conversation was finished to a new conversation.",
            "3082": "Pasa el correo llegado después de la finalización a una conversación nueva."
          },
          "ubicaciones": ["Form"],
          "secuencia": 50
        }
      ],
      "ocultar": []
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_carp_tbl_arreglo",
      "biblioteca": "sanic_carp_wr_js_comandos",
      "botones": [
        {
          "id": "generar",
          "regla": "SanicCarp.Arreglo.puedeGenerar",
          "funcion": "SanicCarp.Arreglo.generar",
          "icono": "sanic_carp_wr_svg_accion",
          "etiqueta": {"1033": "Generate form", "3082": "Generar formato"},
          "tooltip": {
            "1033": "Generates the Excel form F-NIC-0000052 for signature. A draft is frozen on generation.",
            "3082": "Genera el formato F-NIC-0000052 en Excel para la firma. Un borrador se congela al generar."
          },
          "ubicaciones": ["Form"],
          "secuencia": 10
        },
        {
          "id": "desbloquear",
          "regla": "SanicCarp.Arreglo.puedeDesbloquear",
          "funcion": "SanicCarp.Arreglo.desbloquear",
          "icono": "sanic_carp_wr_svg_accion",
          "etiqueta": {"1033": "Unlock", "3082": "Desbloquear"},
          "tooltip": {
            "1033": "Unlocks the agreement data of a frozen draft.",
            "3082": "Desbloquea los datos del acuerdo de un borrador congelado."
          },
          "ubicaciones": ["Form"],
          "secuencia": 20
        },
        {
          "id": "formalizar",
          "regla": "SanicCarp.Arreglo.puedeFormalizar",
          "funcion": "SanicCarp.Arreglo.formalizar",
          "icono": "sanic_carp_wr_svg_accion",
          "etiqueta": {"1033": "Formalize", "3082": "Formalizar"},
          "tooltip": {
            "1033": "Formalizes the frozen draft once the customer signed the printed form.",
            "3082": "Formaliza el borrador congelado cuando el cliente firmó el formato impreso."
          },
          "ubicaciones": ["Form"],
          "secuencia": 30
        },
        {
          "id": "descartar",
          "regla": "SanicCarp.Arreglo.puedeDescartar",
          "funcion": "SanicCarp.Arreglo.descartar",
          "icono": "sanic_carp_wr_svg_alerta",
          "etiqueta": {"1033": "Discard", "3082": "Descartar"},
          "tooltip": {
            "1033": "Discards the draft with the reason typed in the form.",
            "3082": "Descarta el borrador con el motivo escrito en el formulario."
          },
          "ubicaciones": ["Form"],
          "secuencia": 40
        },
        {
          "id": "anular",
          "regla": "SanicCarp.Arreglo.puedeAnular",
          "funcion": "SanicCarp.Arreglo.anular",
          "icono": "sanic_carp_wr_svg_alerta",
          "etiqueta": {"1033": "Void", "3082": "Anular"},
          "tooltip": {
            "1033": "Voids the agreement with the reason typed in the form. It cannot be undone.",
            "3082": "Anula el arreglo con el motivo escrito en el formulario. No se puede revertir."
          },
          "ubicaciones": ["Form"],
          "secuencia": 50
        },
        {
          "id": "reabrir",
          "regla": "SanicCarp.Arreglo.puedeReabrir",
          "funcion": "SanicCarp.Arreglo.reabrir",
          "icono": "sanic_carp_wr_svg_alerta",
          "etiqueta": {"1033": "Reopen agreement", "3082": "Reabrir arreglo"},
          "tooltip": {
            "1033": "Unfreezes the data to correct it, with the reason typed in the form. Managers only.",
            "3082": "Descongela los datos para corregirlos, con el motivo escrito en el formulario. Solo jefatura."
          },
          "ubicaciones": ["Form"],
          "secuencia": 60
        }
      ],
      "ocultar": []
    },
    {
      "clase": "ribbon",
      "tabla": "sanic_carp_tbl_versionplantilla",
      "biblioteca": "sanic_carp_wr_js_comandos",
      "botones": [
        {
          "id": "publicar",
          "regla": "SanicCarp.Plantilla.puedePublicar",
          "funcion": "SanicCarp.Plantilla.publicar",
          "icono": "sanic_carp_wr_svg_accion",
          "etiqueta": {"1033": "Publish version", "3082": "Publicar versión"},
          "tooltip": {
            "1033": "Makes the draft version the current one. The previous one becomes historical.",
            "3082": "Convierte la versión en borrador en la vigente. La actual pasa a histórica."
          },
          "ubicaciones": ["Form"],
          "secuencia": 10
        },
        {
          "id": "vistaprevia",
          "regla": "SanicCarp.Plantilla.puedeVistaPrevia",
          "funcion": "SanicCarp.Plantilla.vistaPrevia",
          "icono": "sanic_carp_wr_svg_accion",
          "etiqueta": {"1033": "Preview", "3082": "Vista previa"},
          "tooltip": {
            "1033": "Generates the form with an existing agreement using this draft version. It saves nothing.",
            "3082": "Genera el formato con un arreglo existente usando esta versión en borrador. No guarda nada."
          },
          "ubicaciones": ["Form"],
          "secuencia": 20
        }
      ],
      "ocultar": []
    }
  ]
}
```

## 3. Decisiones del playbook

| # | Decisión | Por qué |
|---|---|---|
| 1 | `ocultar: []` en las tres tablas y las demás tablas de la app en `tablas_excluidas_de_ocultar` | `diseno/07` §5.5 y DA-15: en la primera versión no se oculta ningún botón estándar (lote C). `app` + exclusiones hace que `--solo-verificar` denuncie una tabla de la app que quede sin bloque por olvido |
| 2 | Solo Tomar lleva `Form` y `HomepageGrid`; el resto, solo `Form` | `diseno/07` §5.6. Tomar genera dos comandos (`...tomar.Grid.Command` y `...tomar.Form.Command`). Los demás gestos leen campos del formulario |
| 3 | Todos los botones llevan `regla` (el diseño nombra `puedeTomar` y deja el resto implícito) | La regla repite el diagrama de estados solo para no mostrar un botón sin sentido; devuelve `true` ante la duda y el servidor decide. En la grilla, `puedeTomar` devuelve `true` (la vista no trae siempre el estado ni el oficial) |
| 4 | `puedeGenerar` oculta el botón en el alta (`getFormType() === 1`) | `diseno/07` dice "siempre"; en un registro sin crear no hay nada que imprimir |
| 5 | Etiquetas 1033 en inglés y 3082 en español, con tildes; tooltips igual | Texto de interfaz, no pasa por BP-PP-197 (README, `herramientas`). Los diálogos del JS van siempre en español (`diseno/07` §5.4) |
| 6 | Íconos: `accion` para Tomar, Finalizar, Unir, Reabrir conversación, Iniciar nueva, Generar, Desbloquear, Formalizar, Publicar y Vista previa; `alerta` para Descartar, Anular y Reabrir arreglo | `diseno/07` §5.6 (DA-10: cuatro SVG compartidos) |
| 7 | Las dos "Reabrir" comparten `id` pero están en tablas distintas | El id completo lleva la tabla: `sanic.carp.conversacion.reabrir` y `sanic.carp.arreglo.reabrir` |
| 8 | `PrimaryControl` en `Form` y `SelectedControlSelectedItemIds` + `SelectedControl` en `HomepageGrid` | Lo genera la herramienta (G-I1b). Las funciones usan `SanicCarp.Comun.contexto(...)` para aceptar ambos |

## 4. Precondiciones

| # | Qué tiene que existir | Cómo se comprueba |
|---|---|---|
| 1 | El web resource `sanic_carp_wr_js_comandos` con las funciones de la sección 2 | `playbooks/web-resource/sanic_carp_wr_js_comandos.md` |
| 2 | Los iconos `sanic_carp_wr_svg_accion` y `sanic_carp_wr_svg_alerta` | `GET webresourceset?$filter=name eq '...'` |
| 3 | La app `sanic_carp_mda_controlarreglospago` con sus 8 tablas | `playbooks/app/controlarreglospago.md` (se construye antes) |
| 4 | Solución no managed e idiomas 1033 y 3082 provisionados | `comprobar_solucion_e_idioma` |

## 5. Cómo se construye

```
python3 herramientas/construir/importar_solucion.py playbooks/importacion/comandos.md
```

Orden (`diseno/07` §7.1): web resource → formularios → `app.py` → **este playbook** → regenerar la metadata del ribbon (abrir la app con `&ribbondebug=true`, Command Checker, *Regenerate ribbon metadata*, después de la última importación) → `app.py --solo-verificar`.

## 6. Verificación

```
python3 herramientas/construir/importar_solucion.py playbooks/importacion/comandos.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `RetrieveEntityRibbon` de las tres tablas | cada botón presente en cada ubicación, con `ModernImage`, su `EnableRule` y `PrimaryControl` en el comando y en la regla de `Form` |
| 2 | Tablas de la app sin bloque | ninguna fuera de `tablas_excluidas_de_ocultar` |
| 3 | Segunda ejecución | `ya_existia` |

Fuera de la herramienta, en Dev, con un oficial y un jefe no administradores (`diseno/07` §9.3 y §12 puntos 4, 8, 9, 12, 17): los 14 gestos en el orden del flujo, cada uno con un caso de error (`CARP-*` en español) y la carrera de Tomar con dos navegadores. **[verificar en Dev]** que en `Form` el botón aparece sin `Mscrm.SelectionCountAtLeastOne` (G-I2) y que `PrimaryControl` llega a la regla.

## 7. Si ya existe

Mismas comprobaciones que la sección 6. Si pasan: `ya_existia`; si no: `difiere` con lo que falta. `--forzar` reimporta sin preguntar.

## 8. Reversa

Reimportar la solución con el `RibbonDiffXml` anterior de la tabla (o vacío). Mientras no llegue a Test o Prod es reversible; el ocultamiento reversible (`ocultar` sin `ocultar_con_hidecustomaction`) no aplica porque no se oculta nada.

## 9. Fuera de alcance

Ocultar botones estándar (lote C, DA-15), el botón Clonar (DA-13), Reasignar y Responder (no son comandos, `diseno/07` §4.2), Registrar pago (Guardar nativo de Cuota).
