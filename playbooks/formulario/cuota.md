# Playbook: formulario · FRM - CARP - Cuota - Principal

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para formulario). El formato sale de la Identidad común y de lo que lee `herramientas/construir/formulario.py` (formato ampliado de `herramientas/README.md`). Componente **propuesto** (fila 12.26, DA-09): no suma al inventario hasta que se confirme.

## 1. Identidad

```json
{
  "tipo_playbook": "formulario",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.26",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0, verificados contra Dev el 2026-10-09. El diseño está en `diseno/07-diseno-app-y-flujos.md` §4.1 y §4.7 (propuesta 12.26, DA-09). Solo `sanic_fechapago` y `sanic_montopagado` son editables (`Columnas.cs`).

Advertencias: etiquetas en los dos LCID, en español y sin tildes (BP-PP-197). `formulario.py` actualiza el Main existente.

## 2. Qué se crea

```json
{
  "tipo": "formulario",
  "nombre": "FRM - CARP - Cuota - Principal",
  "tabla": "sanic_carp_tbl_cuota",
  "descripcion": "Formulario principal de Cuota, para registrar el pago: fecha y monto pagado se guardan juntos. Las demás columnas las escribe el sistema y son de solo lectura.",
  "solo_lectura": false,
  "pestana": {
    "1033": "General",
    "3082": "General"
  },
  "encabezado": [],
  "secciones": [
    {
      "titulo": {
        "1033": "Pago",
        "3082": "Pago"
      },
      "campos": [
        {"nombre": "sanic_nombre", "solo_lectura": true},
        {"nombre": "sanic_numero", "solo_lectura": true},
        {"nombre": "sanic_fechavencimiento", "solo_lectura": true},
        {"nombre": "sanic_monto", "solo_lectura": true},
        {"nombre": "sanic_arregloid", "solo_lectura": true},
        {"nombre": "sanic_fechapago", "solo_lectura": false},
        {"nombre": "sanic_montopagado", "solo_lectura": false}
      ]
    }
  ],
  "bibliotecas": [
    "sanic_carp_wr_js_comandos"
  ],
  "eventos": [
    {"evento": "onsave", "biblioteca": "sanic_carp_wr_js_comandos", "funcion": "SanicCarp.Cuota.alGuardar", "pasar_contexto": true}
  ]
}
```

Decisiones y huecos:

- El pago se registra con Guardar (manual): fecha y monto **juntos**; si falta uno, `CARP-CUO-003`. Solo se acepta con el arreglo Vigente (`CARP-CUO-002`). `OnSave` cancela el autoguardado.
- Se abre al hacer clic en una fila de la subgrilla de cuotas del Arreglo (comportamiento nativo).
- `sanic_monto` y `sanic_montopagado` son decimales: requieren el classid de decimal (G-F5, ya en `formulario.py`).
- El nombre de la función lo eligió este playbook (`diseno/07` §4.7 no lo nombra).
- **Hueco**: la grilla editable en la subgrilla (alternativa A de `diseno/07` §4.7) no la configura la herramienta.

## 3. Precondiciones

| # | Qué tiene que existir | Cómo se comprueba |
|---|---|---|
| 1 | La tabla y las 7 columnas, con etiqueta en 1033 y 3082 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_cuota')/Attributes` |
| 2 | La tabla figura en la solución con `rootcomponentbehavior` 0 | `GET solutioncomponents` |
| 3 | El web resource `sanic_carp_wr_js_comandos` existe y es JavaScript | `GET webresourceset?...` |

## 4. Cómo se construye

```
python3 herramientas/construir/formulario.py playbooks/formulario/cuota.md
```

## 5. Verificación

```
python3 herramientas/construir/formulario.py playbooks/formulario/cuota.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET systemforms?$filter=objecttypecode eq 'sanic_carp_tbl_cuota' and type eq 2` | un solo Main · `name` y `description` los de la sección 2 · activo · no managed |
| 2 | `formxml` | igual al generado, etiquetas en los dos idiomas |
| 3 | Estructura | 1 pestaña, 1 sección, 7 campos, 0 subgrillas, 0 en el encabezado, 1 biblioteca, 1 manejador |
| 4 | Script | `onsave` → `SanicCarp.Cuota.alGuardar` con `passExecutionContext` true |
| 5 | Pertenencia | la tabla figura en la solución con `rootcomponentbehavior` 0 |
| 6 | Segunda ejecución | `ya_existia` |

## 6. Si ya existe

Mismas comprobaciones que la sección 5. Si pasan: `ya_existia`; si no: `difiere` (corrige con `PATCH` y publica).

## 7. Reversa

Se restituye el `formxml` anterior desde la solución exportada. Reversible mientras no llegue a Test o Prod.

## 8. Fuera de alcance

La vista "Cuotas del arreglo", la subgrilla en el Arreglo (`playbooks/formulario/arreglo.md`), la grilla editable.
