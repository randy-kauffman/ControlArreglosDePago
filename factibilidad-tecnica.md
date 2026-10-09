# Factibilidad técnica: Control de Arreglos de Pago

<!-- Plantilla del documento de factibilidad técnica (etapa 1B). La completa el orquestador (skill `validacion`) siguiendo el método de la skill `factibilidad-tecnica`; los agentes `evaluador-solucion` no escriben archivos, solo devuelven una candidata en el formato de retorno definido en el archivo del agente. -->

## 1. Metadatos

- id: 2026-003-control-arreglos-pago
- fecha: 2026-10-07
- panorama: bac-credomatic/arreglos-de-pago
- estado: concluida
- ficha_actualizada: 2026-10-07

<!-- ficha_actualizada = el valor de `actualizado` leído de ficha-solicitud.md en el momento de evaluar. -->

## 2. Requisitos técnicos derivados

Requisitos confirmados por el usuario el 2026-10-07. RF-06 se precisó después de la primera ronda de evaluación.

Funcionales:

- RF-01: capturar automáticamente cada correo de solicitud que llega al buzón del área (hoy grupo de distribución, que pasaría a buzón compartido) y registrarlo como solicitud con fecha y hora de llegada [fuente: ficha §4, §5, §7, §9].
- RF-02: asignar cada solicitud a un oficial, visible para todos [fuente: ficha §5, §7].
- RF-03: registrar la respuesta al cliente (qué y cuándo) y calcular el tiempo de respuesta [fuente: ficha §4, §6].
- RF-04: vista de supervisión con solicitudes sin respuesta y tiempos por oficial [fuente: ficha §6].
- RF-05: cargar el arreglo con todos sus datos (cliente, producto, tipo Temporal o Cancelación, saldos, plazo de hasta 60 cuotas, cuota, fechas, cuenta) y número de arreglo consecutivo por crédito [fuente: ficha §5, §7].
- RF-06: generar el calendario de cuotas y el formato controlado F-NIC-0000052. La plantilla Excel se sube a la solución y cada campo se ubica con un mapeo celda → campo (por ejemplo, "en D15 va el campo X") editable sin código. El resultado se descarga en Excel o PDF. Si hace falta corregir algo a mano, se edita el Excel descargado. Sin SharePoint ni OneDrive [fuente: ficha §7, §8] [fuente: evaluador].
- RF-07: registrar a mano el pago de cada cuota [fuente: ficha §5, §8].
- RF-08: lista de arreglos en riesgo (cuotas vencidas sin pago) [fuente: ficha §6].
- RF-09: anular un arreglo, dejando registro [fuente: ficha §7].
- RF-10: buscar por cédula o por número de préstamo o tarjeta, con el historial completo [fuente: ficha §6].

No funcionales:

- RNF-01: 4 usuarios, todos con Power Apps Premium per user [fuente: panorama §3].
- RNF-02: unas 80 solicitudes y 60 arreglos por semana (unos 3.000 arreglos por año), con hasta 60 cuotas por arreglo [fuente: ficha §5].
- RNF-03: datos "Sensible", prohibidos en SharePoint y OneDrive. Acceso restringido al rol ARP, incluido el buzón y sus registros [fuente: ficha §8] [fuente: panorama §7].
- RNF-04: sin integración con el AS400 [fuente: ficha §8].
- RNF-05: Power Platform con Dataverse; entornos Dev, Test y Prod compartidos y administrados. Office 365 Outlook y Dataverse están en el mismo grupo DLP [fuente: panorama §3, §6] [fuente: evaluador]. Code apps habilitadas en los tres entornos [fuente: panorama §3]. La server-side synchronization con Exchange Online se puede habilitar [fuente: evaluador].
- RNF-06: entrega el 2026-10-31 [fuente: ficha §8].
- RNF-07: regulación externa (SIBOIF, sigilo bancario) sin resolver [fuente: ficha §10].
- Preferencia de diseño: tabla custom propia de clientes, no la tabla Contact [fuente: evaluador].

## 3. Soluciones candidatas

<!-- Veredicto: viable | con-reservas | descartada. -->
<!-- Confianza: pares `componente: nivel; componente: nivel`, con nivel alta | media | baja — un componente por cada skill de plataforma/componente cargada para esa candidata. -->

| Candidata | Componentes | Licencia resultante | Veredicto | Confianza | Motivo |
|---|---|---|---|---|---|
| C1 | Model-driven app + Dataverse (tablas propias, clientes custom, plantilla en columna File y tabla de mapeo) + Power Automate (trigger de buzón compartido de Office 365 Outlook) + generador de documento propio (JS/PCF en el cliente o Custom API con Open XML) | Power Apps Premium per user de los 4 usuarios, que ya la tienen. Flujo asociado a la app, propiedad de la cuenta de servicio licenciada. Sin costo incremental con librerías propias u open source | con-reservas | model-driven: alta; dataverse: media; power-automate: alta; generacion-documento: baja | Cubre RF-01 a RF-10 sin SharePoint ni OneDrive. El Excel de RF-06 tiene dos mecanismos independientes (cliente JS, encaja con React/TS, o plugin con Open XML). Reservas: fidelidad del relleno de la plantilla controlada (spike), salida PDF sin mecanismo nativo y plazo |
| C2 | Canvas app + Dataverse (tablas propias, clientes custom, plantilla y mapeo) + Power Automate (trigger de buzón compartido) + generador de documento (Custom API con Open XML invocada desde canvas, o componente PCF en canvas) | Power Apps Premium per user de los 4 usuarios, que ya la tienen. Flujo en contexto con la cuenta de servicio. Sin costo incremental | con-reservas | canvas: alta; dataverse: media; power-automate: alta; generacion-documento: media; salida-pdf: baja | Cubre RF-01 a RF-10. Canvas no ejecuta JS propio, así que RF-06 sale por Custom API o PCF, con dos caminos independientes. Hay que construir a mano vistas y búsquedas delegables (cota de 180.000 cuotas por año). Reservas: spike de RF-06, PDF y plazo |
| C3 | Power Apps code app (React/TypeScript) + Dataverse (tablas propias, clientes custom, plantilla y mapeo) + Power Automate (trigger de buzón compartido) + relleno del .xlsx en el navegador (Custom API con Open XML como alternativa) | Power Apps Premium per user de los 4 usuarios, que ya la tienen (code apps exigen Premium siempre). Flujo en contexto con la cuenta de servicio. Sin costo incremental | con-reservas | code-apps: media; dataverse: alta; power-automate: media; generacion-documento: baja | La UI es 100 % a medida y RF-06 se resuelve en el propio navegador. Reservas: descarga de archivos de Dataverse desde code apps en preview, lookups con guía en desarrollo, ALM sin Git nativo, PDF y el mayor costo de construcción frente al plazo |
| C4 | Model-driven app + Dataverse (tablas propias, clientes custom) + server-side synchronization del buzón compartido como Queue con mailbox (filtro "All email messages") + Power Automate complementario + generador de documento (Custom API con Open XML como base, JS en el cliente como respaldo) | Power Apps Premium per user de los 4 usuarios, que ya la tienen. Queue y Queue Item no están restringidas. El buzón compartido no requiere licencia de Exchange. El dueño del registro mailbox/queue necesita licencia. Sin costo incremental | con-reservas | model-driven: alta; dataverse: media; server-side-sync: baja; power-automate: media; generacion-documento: media; salida-pdf: baja | RF-01 a RF-04 nativos: cada correo es una actividad Email con su hilo y correlación de respuestas por InReplyTo, también de remitentes desconocidos. Reservas: comportamiento real de la sincronización con el buzón (latencia de 5 a 15 minutos sin SLA, permisos, owner), spike de RF-06, PDF y plazo |

Descartadas sin evaluación detallada (restricción dura): las candidatas con SharePoint como almacén de los datos del arreglo [fuente: panorama §7].

## 4. Integraciones

- Exchange Online, entrada de correos (RF-01):
  - C1, C2 y C3 usan el trigger de buzón compartido del conector Office 365 Outlook. Es estándar y está en el mismo grupo DLP que Dataverse [fuente: evaluador].
  - C4 usa la server-side synchronization. Hechos verificados en Microsoft Learn el 2026-10-07 (páginas completas):
    - Funciona con apps model-driven propias.
    - Solo sincroniza buzones, no grupos de distribución.
    - Un buzón se asigna a un único usuario o Queue, y a una sola instancia de Dataverse.
    - Después de cada ciclo de entrada espera 5 minutos y no tiene SLA.
    - Un buzón compartido no requiere licencia de Exchange.
    - Las queues admiten el filtro "All email messages", que registra todo correo recibido, también de remitentes desconocidos.
- Respuesta al cliente (RF-03): para registrarla de forma automática, la respuesta tiene que salir del buzón compartido (con permiso Send As). En C4 la correlación es nativa (InReplyTo). En C1, C2 y C3 se necesita un flujo sobre Enviados o la acción desde la app.
- Dataverse: único almacén. El conector premium queda cubierto en contexto de app si el flujo se asocia a la app [fuente: skill power-platform-evaluar §3.1].
- AS400: sin integración [fuente: ficha §8].
- Generación de documentos (RF-06):
  - La plantilla vive en una columna File de Dataverse, junto con una tabla de mapeo celda → campo. Para el calendario de cuotas hace falta un mapeo de bloque repetido.
  - Hay dos mecanismos independientes:
    - JavaScript en el cliente (PCF, web resource, custom page o code app) con una librería de hojas de cálculo.
    - Custom API con plugin y Open XML SDK como ensamblado dependiente (hasta 16 MB, sandbox, 2 minutos).
  - No sirven:
    - La plantilla Excel nativa de Dataverse, porque es para vistas de listas.
    - El conector Excel Online (Business) y las conversiones a PDF de Power Automate, porque operan sobre OneDrive o SharePoint.
- Salida PDF: no hay mecanismo nativo sin OneDrive ni SharePoint. Las opciones son:
  - (a) El usuario descarga el Excel y lo guarda como PDF.
  - (b) Un PDF generado en el navegador con una librería, que re-maqueta el formato.
  - (c) Una librería administrada dentro del plugin (fidelidad y costo no verificados).
  - (d) Una Azure Function, que saca datos Sensible de Dataverse y requiere aprobación de seguridad y un conector de clasificación DLP desconocida.

## 5. Datos, seguridad y cumplimiento

- Datos "Sensible" (cédula, cuenta, contacto, deuda, cuerpo y adjuntos de los correos), plantilla y mapeo: solo en Dataverse. Se cumple la prohibición de SharePoint y OneDrive [fuente: panorama §7].
- Acceso restringido al rol ARP:
  - Security role del área, owning team o business unit ARP, y seguridad por columna para cédula y cuenta.
  - Los registros creados por el flujo (C1 a C3) o por la sincronización (C4: Queue, Queue Items y correos) deben quedar asignados al team ARP.
  - El acceso al buzón se controla además en Exchange.
- Auditoría de Dataverse configurable por entorno, tabla y columna.
- El historial de ejecuciones de los flujos puede exponer el cuerpo de los correos: hay que activar secure inputs/outputs.
- Documento descargado: el Excel o PDF con datos Sensible sale a la estación del oficial. Es inherente a imprimir para firma y es el mismo riesgo que hoy. Hay que confirmar con TI si la carpeta Descargas está redirigida a OneDrive (Known Folder Move), porque eso llevaría el documento a OneDrive.
- Reproducibilidad del documento firmado: si se regenera a pedido, un cambio de plantilla o de mapeo altera los documentos históricos. Hay que versionar la plantilla y el mapeo, o guardar el documento generado en Dataverse.
- Un error en el mapeo editable imprime datos en la celda equivocada de un documento legal. Conviene vista previa, versionado y registro de quién cambió el mapeo.
- Tablas restringidas: no usar Case, SLA, Routing rule set, Entitlement ni Knowledge article. Queue y Queue Item están permitidas (verificado el 2026-10-07 en la página completa de restricted entities).
- Regulación externa (SIBOIF, sigilo bancario), residencia y retención: sin definir [fuente: ficha §10]. Afectan por igual a todas las candidatas.

## 6. Restricciones duras

- Prohibido SharePoint y OneDrive para datos, plantilla y documento generado [fuente: panorama §7]. Descarta las candidatas con SharePoint como almacén, el conector Excel Online (Business) y las conversiones a PDF por esos servicios.
- Sin Case, SLA ni demás tablas restringidas de Dynamics 365.
- Plugin y Custom API: sandbox solo con código administrado y sin sistema de archivos, operación síncrona de hasta 2 minutos, ensamblados dependientes de hasta 16 MB.
- La server-side synchronization (C4) solo funciona con buzones: el buzón compartido es precondición. Además, un buzón se asigna a una sola instancia de Dataverse, así que Dev y Test necesitan buzones de prueba propios.
- Model-driven y code apps son Premium siempre. Los entornos administrados exigen Premium para todo uso; los 4 usuarios ya la tienen [fuente: panorama §3, §6].
- Flujos propiedad de la cuenta de servicio licenciada y asociados a la app [fuente: panorama §6].
- Canvas (C2): delegación de 500 registros por defecto y 2.000 como máximo; búsquedas e historiales delegables.
- Code apps (C3): sin Git nativo y sin restricción por IP de los assets.
- El formato F-NIC-0000052 v06 tiene 48 filas de calendario y totales hasta la cuota 36. Hay arreglos de hasta 60 cuotas, así que hace falta una versión de plantilla aprobada por su dueño, Héctor Romero [fuente: ficha §5, §8].

## 7. Incógnitas técnicas

| Incógnita | Por qué importa | Cómo resolverla | Bloqueante |
|---|---|---|---|
| ¿El mecanismo elegido para RF-06 rellena la plantilla F-NIC-0000052 conservando estilos, celdas combinadas, fórmulas, cláusulas y área de impresión, con un bloque repetido de hasta 60 cuotas? | Es un documento controlado que se firma | Spike de 1 a 2 días con la plantilla real en Dev. Si falla un mecanismo, se prueba el otro (cliente JS o plugin con Open XML) | no |
| ¿Basta que el PDF salga de "descargar el Excel y guardarlo como PDF", o se exige un botón de PDF con fidelidad? | No hay mecanismo nativo de PDF sin OneDrive ni SharePoint. Exigirlo suma un componente (librería o Azure) con costo, DLP o aprobación de seguridad | Confirmarlo con Deyanira Ramírez y Héctor Romero | no |
| Versión de la plantilla con 60 filas y totales corregidos, y fecha de formalización escrita como valor fijo (no `=TODAY()`) | Cambiar el formato controlado requiere aprobación de su dueño | Acordar una v07 con Héctor Romero | no |
| Perfil C#/.NET del equipo, si se elige el plugin con Open XML | El pro-code confirmado es React/TS. Sin C# se usa el mecanismo en el cliente | Confirmación del equipo | no |
| C4: comportamiento real de la sincronización con el buzón (permisos, Send As, dueño de la Queue con licencia, correlación de respuestas) | La habilitación está confirmada, pero el comportamiento con el buzón real no se probó | Prueba en Dev o Test con un buzón compartido de prueba | no |
| C3: descarga de archivos de Dataverse desde code apps (en preview según la referencia) y lookups con guía en desarrollo | Afectan el diseño de RF-06 y de la relación cliente-arreglo | Spike en el entorno. Plan B: Custom API que devuelva el archivo | no |
| Buzón compartido (hoy es un grupo de distribución), cuenta de servicio con acceso, Send As y restricción al rol ARP | Sin buzón compartido no hay RF-01 en ninguna candidata | Pedirlo a mensajería/TI | no |
| ¿La carpeta Descargas está redirigida a OneDrive (Known Folder Move)? | Llevaría el documento con datos Sensible a OneDrive | Consulta a TI o Seguridad | no |
| Capacidad de Dataverse (base de datos y archivos) y convivencia en los entornos compartidos (registro de ensamblados, pool de requests, pipelines) | Hay correos con adjuntos y entornos compartidos | Reporte de capacidad y consulta al administrador | no |
| Regulación externa (SIBOIF, sigilo bancario) | Puede exigir auditoría, retención o ubicación; afecta a todas las candidatas | Consulta a Cumplimiento [fuente: ficha §10] | no |

Nota sobre la incógnita que el evaluador de C3 marcó como bloqueante ("¿están habilitadas las code apps en los entornos?"): el panorama ya la responde. Las code apps están habilitadas en Dev, Test y Prod de Nicaragua, confirmado el 2026-10-03 [fuente: panorama §3]. Por eso no figura en la tabla.

## 8. Orden de magnitud

talla: M

Las 4 candidatas quedan en M (1 a 3 meses).

Componentes de la talla:
- Modelo de 6 a 9 tablas: clientes custom, solicitudes, respuestas, arreglos, cuotas, pagos, configuración de plantilla y mapeo.
- Seguridad por team ARP en entornos compartidos.
- Flujo de entrada de correos, o configuración de la sincronización en C4.
- Generación del calendario de hasta 60 cuotas.
- Vistas de supervisión y de arreglos en riesgo.
- Un componente pro-code para RF-06, con spike de fidelidad y editor de mapeo.
- Despliegue coordinado.

Lo que la empuja hacia arriba:
- Si se exige un PDF fiel, hacia el extremo alto de M o hacia L.
- En C3, el costo de construir toda la UI a mano y el ALM sin Git nativo.
- En C2, la UI y las búsquedas delegables a mano.

Ninguna candidata entra completa en los unos 17 días hábiles hasta el 2026-10-31. Los evaluadores coinciden en que el plazo exige un primer tramo, del orden de RF-01 a RF-05.

## 9. Riesgos técnicos

- Plazo: una solución de talla M contra unos 17 días hábiles, con dependencias de terceros (buzón compartido, administrador, dueño del formato).
- Fidelidad del documento controlado al rellenarlo por código, y la expectativa de un PDF idéntico al formato.
- Errores en el mapeo editable que impriman datos en la celda equivocada de un documento que se firma.
- Reproducibilidad de los documentos firmados si cambian la plantilla o el mapeo.
- Fuga de datos Sensible si alguien "resuelve" RF-06 con Excel Online, OneDrive o un servicio externo. También por descargas redirigidas a OneDrive.
- Captura de correos: fallas silenciosas del trigger o de la sincronización van contra el "100 % respondido". Hace falta monitoreo.
- Seguridad: registros creados por la cuenta de servicio o por la sincronización que no queden asignados al team ARP.
- Consecutivo de arreglo por crédito con oficiales concurrentes: se mitiga con una clave alternativa (crédito + número).
- Entornos compartidos: registro de ensamblados, PCF y pool de requests conviviendo con otras soluciones.
- C3: dependencia de capacidades en preview y del ALM sin Git nativo. C4: latencia de 5 a 15 minutos sin SLA.

## 10. Conclusión

resultado: ⚠️

Fundamento: no queda ninguna incógnita bloqueante y las 4 candidatas quedan con veredicto con-reservas, todas talla M. Ninguna tiene todos sus componentes en confianza alta, porque la generación de documentos y la salida PDF no tienen referencia dedicada y la sección 7 tiene incógnitas no bloqueantes. Por la regla de conclusión, el resultado debe ser ⚠️.

Condiciones técnicas:
- Spike de RF-06 con la plantilla real antes de comprometer el diseño, validado por Héctor Romero.
- Definir con la decisora y el dueño del formato si el PDF puede salir del Excel descargado.
- Versión de plantilla con 60 cuotas aprobada por Héctor Romero.
- Buzón compartido creado y con permisos.
- Para cumplir el 2026-10-31, acotar un primer tramo, del orden de RF-01 a RF-05.

La elección entre candidatas queda para la etapa 2.
