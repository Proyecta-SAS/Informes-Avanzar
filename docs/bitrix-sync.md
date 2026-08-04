# Sincronizacion Bitrix

## Regla principal

La aplicacion web no debe consultar Bitrix en cada carga. Bitrix es la fuente externa, PostgreSQL es el snapshot operativo local y los paneles consultan PostgreSQL.

## Configuracion

Variable requerida:

```text
BITRIX_WEBHOOK_URL=https://tuinstancia.bitrix24.es/rest/USUARIO_ID/TOKEN/
```

El webhook entrante de Bitrix debe tener scopes:

- `crm`
- `user`

## Pipelines iniciales

Cada pipeline local corresponde a un `CATEGORY_ID` de Bitrix:

| Slug | Categoria Bitrix | Dominio |
| --- | ---: | --- |
| `rch_comercial` | 8 | comercial |
| `rch_operativa` | 10 | operaciones |
| `pnnc_comercial` | 26 | comercial |
| `pnnc_operativa` | 28 | operaciones |

El sistema debe permitir agregar mas pipelines desde base de datos sin cambiar codigo.

## Metodos REST usados

- `crm.dealcategory.stage.list`: etapas por pipeline.
- `crm.deal.list`: deals por pipeline.
- `batch`: paginacion masiva de deals.
- `crm.deal.get`: sincronizacion puntual de un deal.
- `user.get`: responsables y usuarios.
- `crm.deal.update`: escritura hacia Bitrix cuando se permita.
- `crm.timeline.comment.add`: comentarios en timeline.
- `crm.timeline.comment.list`, `crm.activity.list`: trazabilidad.
- `tasks.task.list`: tareas, si el webhook incluye permisos para tareas.

## Sincronizacion completa

1. Bloquea ejecuciones simultaneas con una llave global.
2. Lista pipelines activas.
3. Descarga etapas.
4. Descarga todos los deals.
5. Descarga responsables.
6. Abre transaccion.
7. Reemplaza el snapshot de esa pipeline.
8. Inserta datos normalizados y payloads crudos.
9. Recalcula resumenes.
10. Marca la sincronizacion como `succeeded` o `failed`.

La completa reconcilia mejor porque detecta eliminados y deals movidos de categoria.

## Sincronizacion incremental

1. Busca la ultima sincronizacion exitosa por pipeline.
2. Consulta Bitrix con `>=DATE_MODIFY`.
3. Hace upsert de deals modificados.
4. Reemplaza datos dependientes solo de esos deals.

La incremental no elimina registros que salieron de la pipeline. Por eso debe correr una completa periodica.

## Campos personalizados

Los `UF_CRM_*` deben manejarse por pipeline o dominio. En este proyecto se reserva `bitrix.pipelines.field_map` como JSONB para mapear campos sin recompilar la aplicacion.

Adicionalmente:

- `bitrix.custom_fields` registra el catalogo de campos personalizados detectados.
- `bitrix.entity_custom_values` guarda valores consultables por campo.
- `bitrix.entity_snapshots.custom_fields` conserva todos los personalizados como JSONB.

### Campos requeridos por Fuerza Comercial Diego

La sincronizacion resumida de negocios debe solicitar y conservar estos campos para que los paneles mensuales no queden incompletos:

- `UF_CRM_1676419915`: mes de radicacion.
- `UF_CRM_1737653376`: ano de las pipelines operativas.
- `UF_CRM_1737654190`: ano de las pipelines comerciales.

Los valores JSON recibidos de Bitrix se sanean de caracteres nulos antes de guardarlos en PostgreSQL, porque `jsonb` no admite `\u0000`. Las respuestas `429 Too Many Requests` se reintentan con espera progresiva para no dejar incompleta una pipeline extensa.
