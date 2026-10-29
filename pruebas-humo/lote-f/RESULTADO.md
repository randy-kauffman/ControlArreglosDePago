# Lote F: iconos propios de la app (Dev, 2026-10-28)

Pedido del usuario (2026-10-28): los iconos del menú izquierdo y de los botones eran genéricos. Diseño en `diseno/07` §13 (DA-22, reemplaza a DA-10). Inventario: fila 12.27 de `diseno/06`.

## Qué se construyó

| Qué | Cuántos | Herramienta | Resultado en Dev |
|---|---|---|---|
| Web resources SVG `sanic_carp_wr_svg_*` | 41 (1 app, 20 sitemap, 13 comandos, 7 tablas) | `web_resource.py` | 40 `creado` y `app` `actualizado` (contenido nuevo, publicado); segunda pasada `--solo-verificar`: 41 `ya_existia` |
| Icono de las 7 tablas propias (`IconVectorName`) | 7 | `icono_tabla.py` (nueva, 10 pruebas) | `actualizado` (PUT de la definición completa con `MSCRM.MergeLabels` + `PublishXml`); después `ya_existia`. Los 7 playbooks de tabla siguen `ya_existia` con `tabla.py --solo-verificar` (el PUT no cambió nada más) |
| Sitemap: un icono por entrada y títulos 1033 en inglés | 4 áreas, 5 grupos, 20 entradas | `app.py --completar` | `actualizado`, `ValidateApp` sin errores; después `ya_existia`. El sitemap publicado tiene 20 `VectorIcon` distintos |
| Ribbon: un icono por botón | 13 botones | `importar_solucion.py` | `creado` (importjob `4b79ed13-b94c-4d12-ad3d-d88f0c368090`); después `ya_existia` |
| Steps del ensamblado | 42 | (el import) | El import apagó los 37 steps propios y la herramienta los volvió a encender. Relectura: 42 de 42 en `statecode` 0 |

Suite de las herramientas: 1527 OK (1517 + 10 de `icono_tabla.py`).

Los cuatro iconos viejos (`app` cambió de dibujo; `lista`, `accion` y `alerta`) siguen en el entorno: `lista`, `accion` y `alerta` quedan sin uso. No se borró nada.

## Vista previa

Abrí en el navegador el archivo local `webresources/svg/vista-previa.html`. Muestra cada icono a su tamaño real, a 48 px y en fondo oscuro, con su nombre en español y en inglés. No se sube al entorno.

## Lista para probar en el navegador

Antes de empezar: **Ctrl+F5** en la app. Si los botones siguen con el icono viejo, abrí la app con `&ribbondebug=true` al final de la URL, entrá a **Command Checker** y elegí **Regenerate ribbon metadata**; esperá unos minutos y volvé a cargar con Ctrl+F5.

App: https://org36e60d9d.crm.dynamics.com/main.aspx?appid=f13895ff-9bcc-f111-a05b-00224808ffcd

1. **Menú (como User 8, Oficial).** Las 13 entradas de Conversaciones y Clientes y arreglos tienen cada una su icono, ninguno repetido: sobre con persona, sobre con persona punteada, sobre abierto, sobre con punto, burbuja con cumplido, tarjeta de contacto, documento con lápiz, documento con cumplido, documento con advertencia, banco, tarjeta, caja de archivo, pila de documentos.
2. **Menú (como User 9, Jefe).** Además: sobre con reloj, burbuja con cruz, sobre con cruz, avión de papel, documento con reloj de arena, tablero con barras, hoja de cálculo.
3. **Idioma.** En Configuración personal → Idiomas, cambiá a **English**: los títulos del menú salen en inglés (por ejemplo *My pending conversations*, *At-risk agreements*, *Supervision dashboard*) y los botones también (*Take*, *Generate form*, *Publish version*). Volvé a **Español**: todo en español.
4. **Botones de Conversación** (formulario de una conversación abierta sin asignar): Tomar (persona con más), Finalizar (círculo con cumplido), Unir a otra conversación (dos caminos que se juntan). En una finalizada: Reabrir (flecha que vuelve) e Iniciar conversación nueva (burbuja con más).
5. **Botones de Arreglo** (un Borrador): Generar formato (documento con flecha hacia abajo), Formalizar (pluma con línea de firma), Descartar (papelera), Desbloquear (candado abierto, en uno congelado). En un Vigente: Anular (círculo tachado). En un Terminado, como Jefe: Reabrir arreglo (documento con flecha que vuelve).
6. **Botones de Versión de plantilla** (como Jefe, una versión en Borrador): Publicar versión (flecha hacia arriba desde la bandeja) y Vista previa (ojo).
7. **Tablas.** En un formulario de Arreglo, el lookup de Cliente muestra el icono de persona; en la búsqueda global y en "Recientes" cada tabla del proyecto tiene su icono (persona, dos burbujas, documento con firma, monedas, documento con flecha, capas, dos celdas).
8. **Tema oscuro o alto contraste** (si lo tenés activado en Windows): los iconos cambian de color con el texto, no quedan negros sobre fondo oscuro.

Si algún icono no se entiende o no te gusta, decime cuál (por su nombre en la vista previa) y lo redibujo.
