# Playbook: choice-global · sanic_carp_ch_tipomapeo

## 1. Identidad

```json
{
  "tipo_playbook": "choice-global",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "2.8",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. El componente está definido en `diseno/02-diccionario-datos.md` §3.1. El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas.

Advertencias: el idioma base del entorno de desarrollo es 1033. Las etiquetas son las mismas, en español y sin tildes (BP-PP-197), en los dos idiomas. La descripción va en prosa con tildes: no es un nombre visible.

## 2. Qué se crea

```json
{
  "tipo": "choice-global",
  "nombre": "sanic_carp_ch_tipomapeo",
  "displayname": {
    "1033": "CH - CARP - Tipo de mapeo",
    "3082": "CH - CARP - Tipo de mapeo"
  },
  "descripcion": "Tipo de una fila del mapeo celda → campo.",
  "opciones": [
    {
      "valor": 159460001,
      "etiqueta": {
        "1033": "Valor simple",
        "3082": "Valor simple"
      },
      "descripcion": "Reemplaza el contenido de la celda, incluida una fórmula"
    },
    {
      "valor": 159460002,
      "etiqueta": {
        "1033": "Total calculado",
        "3082": "Total calculado"
      },
      "descripcion": "Total que calcula el generador desde los datos"
    },
    {
      "valor": 159460003,
      "etiqueta": {
        "1033": "Columna de bloque de cuotas",
        "3082": "Columna de bloque de cuotas"
      },
      "descripcion": "Columna del bloque repetido de cuotas; la celda es la de la primera cuota"
    }
  ]
}
```

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.1), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Todos los valores están en el rango del publisher | `159460000 ≤ valor < 159470000` |
| 3 | Cada idioma de `idiomas` está provisionado, y la lista incluye 1033 | `[1033, 3082]` |
| 4 | Nombres visibles sin tildes ni ñ (BP-PP-197) y display name con el patrón `CH - CARP - Nombre` | los de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.1 "Choice global", con el contrato de `references/herramientas/patrones.md`.

```
python3 herramientas/construir/choice_global.py playbooks/choice-global/sanic_carp_ch_tipomapeo.md
```

## 5. Verificación

```
python3 herramientas/construir/choice_global.py playbooks/choice-global/sanic_carp_ch_tipomapeo.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET GlobalOptionSetDefinitions(Name='sanic_carp_ch_tipomapeo')` | 200 · `IsGlobal = true` · `OptionSetType = Picklist` · `IsManaged = false` |
| 2 | Display name en 1033 y en 3082, y descripción | los de la sección 2, ninguna etiqueta en otro idioma |
| 3 | Opciones, en orden, con valor, etiqueta por idioma y descripción | `159460001 = Valor simple`, `159460002 = Total calculado`, `159460003 = Columna de bloque de cuotas`; ninguna más |
| 4 | Pertenece a la solución de la sección 1 | exactamente una fila en `solutioncomponents` |
| 5 | Segunda ejecución de la herramienta sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 6 | Muestra real: `python3 herramientas/construir/muestra_choice.py --guardar` (exporta la solución y compara todos los choices) | XML exportado en `playbooks/choice-global/muestras/sanic_carp_ch_tipomapeo.solucion.xml`, con las mismas opciones y etiquetas |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE GlobalOptionSetDefinitions(Name='sanic_carp_ch_tipomapeo')`, posible solo mientras ninguna columna lo use. **Irreversible desde que tenga uso**: el nombre lógico `sanic_carp_ch_tipomapeo`, y los valores `159460001` a `159460003` una vez que haya datos con ellos; nunca se renumeran ni se reutilizan (BP-PP-193). Una opción que deja de usarse se retira de las vistas, no se borra.

## 8. Fuera de alcance

La columna `sanic_tipo` de Mapeo de celda que usa este choice: nace con el playbook de su tabla (inventario 3.6). El código C# no usa los literales numéricos: usa el `enum` que genera `herramientas/codigo/generar_enums.py` desde este playbook. No hay opciones reservadas para fases posteriores.
