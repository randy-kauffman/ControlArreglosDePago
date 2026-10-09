# Charter: Control de Arreglos de Pago

## 1. Metadatos

- id: 2026-003-control-arreglos-pago
- fecha: 2026-10-07
- estado: emitido
- definicion_fecha: 2026-10-07
- sponsor: Deyanira Ramírez, Vice Gerente de Cobranza (solicitante y decisora)
- referente de procesos: Héctor Romero, jefe de Arreglos de Pago

## 2. Objetivos y criterios de éxito

**Objetivo.** El área de Arreglos de Pago controla de punta a punta las solicitudes de arreglo que llegan por correo y los arreglos que formaliza. Lo hace con seguimiento de respuestas, de cuotas y del historial por cliente, sin Excel disperso.

**Criterios de éxito**

1. El 100 % de los correos de solicitud de arreglo recibe respuesta. Hoy hay correos que nunca se responden y no se miden.
2. El tiempo de respuesta de cada correo es visible y medible. Hoy ronda las 3 horas en promedio.
3. Cero quejas de clientes por correos de solicitud sin responder. Hoy hay 2 o 3 por mes.
4. El jefe del área comprueba que todos los correos se respondieron sin revisarlos uno por uno.
5. El área cuenta con una lista de los arreglos en riesgo para revisarlos.
6. Un oficial busca por cédula o por número de préstamo o tarjeta y ve al instante todos los arreglos del cliente, vigentes e históricos, con su estado.

## 3. Alcance y exclusiones

**Incluido**

- Recepción de los correos que llegan al buzón compartido del área, agrupados en conversaciones de ida y vuelta, asignación a un oficial, respuesta al cliente desde la solución con archivos adjuntos e imágenes, finalización de la conversación con su resultado y medición de tiempos.
- Supervisión de los correos sin respuesta y de los tiempos por oficial.
- Registro de clientes y del arreglo con todos los datos del formato F-NIC-0000052, para préstamos y tarjetas, con su calendario de hasta 60 cuotas. El arreglo se prepara como borrador, se puede descartar si no se concreta y se formaliza cuando el cliente firma.
- Filtro por producto crediticio (préstamo o tarjeta) en las listas de arreglos.
- Generación del formato F-NIC-0000052 en Excel a partir de una plantilla, para imprimir y firmar. El dueño del formato configura la ubicación de cada dato sin programación.
- Registro manual de pagos por cuota, lista de arreglos en riesgo y anulación.
- Historial por cliente, con búsqueda por cédula o por número de préstamo o tarjeta.
- Un mockup funcional simulado (con datos de ejemplo, descartable) para que negocio valide la propuesta antes de construir. Validado el 2026-10-08.

**Excluido**

- El flujo físico: la firma presencial del formato y el pago de las cuotas por parte del cliente.
- La migración de los Excel de arreglos existentes. El control arranca con los arreglos nuevos.
- La integración con los sistemas del core (AS400). Saldos y pagos se cargan a mano.
- La descarga directa en PDF. Alcanza con el Excel descargado, que se puede guardar como PDF.
- La gestión de cobro posterior a la anulación, que sigue por Cobranza.
- Guardar una copia del documento generado. Se regenera desde los datos registrados.

**Requisitos principales**

- Capturar automáticamente cada correo que llega al buzón compartido, incluidos los de remitentes que todavía no son clientes, con su fecha y hora de llegada. Las respuestas del cliente en el mismo hilo se agregan solas a su conversación; los correos sueltos sobre el mismo tema se unen a mano.
- Asignar cada conversación a un oficial, con la asignación visible para todo el área. Solo el jefe reasigna.
- Responder al cliente desde la solución, en el mismo hilo, con adjuntos e imágenes. Queda registro de qué se respondió y cuándo, y se mide el tiempo de cada respuesta y el tiempo total hasta finalizar.
- Finalizar cada conversación con su resultado: con arreglo de pago, sin arreglo de pago o sin respuesta. Si el cliente vuelve a escribir, el oficial decide si la reabre o abre una nueva.
- Una vista de supervisión con los correos sin respuesta, su antigüedad y los tiempos por oficial.
- Clientes en una tabla propia. El arreglo lleva todos los datos del formato, un número consecutivo por crédito sin duplicados, que se fija al formalizar, y un calendario de hasta 60 cuotas.
- Generación del formato en Excel desde la plantilla vigente. Solo el jefe del área administra la plantilla.
- Registro manual de pagos y lista de arreglos en riesgo (cuota vencida, sin pago registrado desde el día siguiente al vencimiento). También anulación con motivo e historial por cliente.
- Cuatro usuarios (tres oficiales y el jefe), con licencias ya asignadas.
- Datos de clientes clasificados "Sensible", guardados solo en la plataforma aprobada y con acceso restringido al área.
- Retención de 2 años desde que termina el arreglo.
- Se acepta una demora de 5 a 15 minutos en la llegada de los correos.

## 4. Entregables y fases

**Fase única (F1).** Entrega la solución completa en un solo corte, en este orden:

1. Mockup funcional simulado, validado por negocio.
2. Aplicación con conversaciones de correo, supervisión, clientes, arreglos (borrador y formalización) y cuotas.
3. Generación del formato en Excel y administración de la plantilla.
4. Pagos, lista de riesgo, anulación e historial.

**Solución en breve.** Es una aplicación de gestión sobre la plataforma corporativa obligatoria (Power Platform con Dataverse), con dos perfiles: oficial y jefe.

- **Buzón y conversaciones.** El buzón compartido del área se sincroniza con la plataforma. Cada correo queda en su conversación y las respuestas salen desde la aplicación, en el mismo hilo, con sus adjuntos.
- **Aislamiento de datos.** Los datos quedan aislados en una unidad propia del área, dentro del entorno compartido.
- **Formato firmado.** El formato se genera desde una plantilla versionada. Al imprimirlo para firma, los datos del borrador se bloquean. Al formalizarlo, quedan congelados para que el documento firmado sea reproducible. Solo el jefe puede reabrir un arreglo formalizado, y debe indicar un motivo.
- **Retención.** Los 2 años de retención se aplican con borrado programado.

## 5. Plan y roles

**Duración estimada:** unos 2,5 meses, con una persona dedicada al 100 %. La estimación creció respecto de la versión anterior de este documento por los cambios que pidió negocio al validar el mockup.

**Orden de trabajo**

1. Mockup funcional simulado y su validación con negocio (completado el 2026-10-08).
2. Pruebas técnicas tempranas de la plantilla y de la sincronización del buzón.
3. Construcción.

**Roles**

- Desarrollador de soluciones de automatización (1 persona): dedicación del 100 %.
- Validación de negocio (dedicación puntual):
  - Deyanira Ramírez y Héctor Romero validan el mockup.
  - Héctor Romero además aprueba la plantilla v07 y las reglas de cuotas.
- Administrador del entorno y mensajería (dedicación puntual): unidad del área, permisos, buzón compartido y despliegue.

## 6. Riesgos principales

| Riesgo | Mitigación |
|---|---|
| Plazo: la duración estimada supera la fecha pedida (2026-10-31). | La Vice Gerente confirma por escrito una nueva fecha, un recorte o fases antes de construir. |
| Cambios de negocio al ver el mockup. | Ocurrieron y ya se incorporaron antes de construir: conversaciones, borradores, adjuntos y filtro por producto. |
| Datos sensibles en adjuntos e imágenes de los correos. | Quedan dentro del mismo aislamiento y de la misma retención de 2 años que el resto de los datos. |
| Fidelidad del formato controlado generado desde la plantilla. | Prueba técnica temprana con la plantilla real y una alternativa de generación. |
| Aprobación de la plantilla con 60 cuotas y de las reglas de cuotas. | Pedirlas junto con la validación del mockup. |
| Comportamiento real de la sincronización del buzón. | Prueba temprana con un buzón de prueba y una alternativa basada en automatización de correo. |
| Normativa bancaria (SIBOIF, sigilo bancario) que cambie la auditoría o la retención. | Consulta formal a Cumplimiento antes de cerrar el diseño de datos. |
| Acceso a datos sensibles desde otras soluciones del entorno compartido. | Unidad propia del área y verificación de permisos con el administrador. |
| Excel existentes con datos sensibles en SharePoint. | Plan escrito de restricción o retiro, a cargo de Héctor Romero. |
| Adopción: las respuestas deben salir solo desde la aplicación y con asignación formal. | Validación del mockup, capacitación y gestión del cambio. |

## 7. Supuestos y dependencias

**Supuestos**

- Un pago parcial registrado saca la cuota de la lista de riesgo.
- Las solicitudes que no terminan en arreglo se conservan 2 años desde su cierre.
- Los oficiales responden siempre desde la solución.
- El equipo tiene las capacidades técnicas necesarias.
- Las reglas de fechas de vencimiento, de cuándo un arreglo queda terminado y de cantidad de pagos por cuota se confirman con Héctor Romero al validar el mockup.

**Dependencias**

- Deyanira Ramírez y Héctor Romero validan el mockup antes de las pruebas técnicas (cumplido el 2026-10-08).
- El administrador del entorno confirma el tamaño máximo y los tipos de archivos adjuntos permitidos.
- Mensajería o TI crea el buzón compartido que reemplaza al grupo de distribución.
- El administrador habilita la sincronización del buzón en los entornos.
- Héctor Romero aprueba la plantilla v07 y el plan para los Excel en SharePoint.
- Cumplimiento responde sobre SIBOIF y sigilo bancario.
- Deyanira Ramírez confirma por escrito la fecha, un recorte o fases.

## 8. Aprobación del sponsor

- rol: 
- fecha: 
- comentarios:
