using System.Text.Json;
using InformesAvanzar.Api.Bitrix;
using Npgsql;
namespace InformesAvanzar.Api.Data;
public static class OrganizationQueries {
 private sealed record DepartmentItem(string Id,string Name,string? ParentId,string? HeadId);
 private static readonly string[] CommercialRoleLabels = ["coordinator_rch","coordinator_pnnc","leader_rch","leader_pnnc","custom"];
 private static readonly string[] ManagedReports = ["informe_general_comercial"];
 private static readonly string[] CommercialReportCatalog = ["informe_general_comercial","fuerza_comercial_diego","rch_comercial","rch_operativa","pnnc_comercial","pnnc_operativa","informe_gerencia_2026_2027"];
 private static readonly string[] CommercialBlockCatalog = ["radicated_values","advisor_negotiations","coordinator_values","leader_values","coordinator_detail","leader_detail","advisor_commissions","portfolio_state","portfolio_collected","funnel_insolvency","funnel_rch","commercial_possible_close_rch","commercial_possible_close_pnnc"];
 private static readonly string[] SharedManagedBlocks = ["radicated_values","advisor_negotiations","leader_values","advisor_commissions","portfolio_state","portfolio_collected"];
 public static bool IsCommercialRoleLabel(string? roleLabel)=>!string.IsNullOrWhiteSpace(roleLabel)&&CommercialRoleLabels.Contains(roleLabel,StringComparer.OrdinalIgnoreCase);
 public static string NormalizeCommercialRoleLabel(string? roleLabel)=>IsCommercialRoleLabel(roleLabel)?roleLabel!.Trim().ToLowerInvariant():"leader_rch";
 private static string[] BlocksForCommercialRole(string roleLabel){
  var blocks=roleLabel.StartsWith("leader_",StringComparison.OrdinalIgnoreCase)?SharedManagedBlocks.Where(code=>code!="leader_values").ToArray():SharedManagedBlocks;
  return roleLabel.EndsWith("_pnnc",StringComparison.OrdinalIgnoreCase)?[..blocks,"funnel_insolvency","commercial_possible_close_pnnc"]:[..blocks,"funnel_rch","commercial_possible_close_rch"];
 }
 public static async Task EnsureSchemaAsync(NpgsqlDataSource ds,CancellationToken ct){await using var connection=await ds.OpenConnectionAsync(ct);await using var command=new NpgsqlCommand("""ALTER TABLE bitrix.departments ADD COLUMN IF NOT EXISTS head_bitrix_id text; CREATE TABLE IF NOT EXISTS reporting.organization_access (department_id text PRIMARY KEY, role_label text NOT NULL DEFAULT 'viewer', visible_reports text[] NOT NULL DEFAULT ARRAY[]::text[], visible_blocks text[] NOT NULL DEFAULT ARRAY[]::text[], user_id uuid NULL REFERENCES auth.users(id) ON DELETE SET NULL, updated_at timestamptz NOT NULL DEFAULT now()); ALTER TABLE reporting.organization_access ADD COLUMN IF NOT EXISTS visible_blocks text[] NOT NULL DEFAULT ARRAY[]::text[]; ALTER TABLE reporting.organization_access ADD COLUMN IF NOT EXISTS user_id uuid NULL REFERENCES auth.users(id) ON DELETE SET NULL; CREATE INDEX IF NOT EXISTS ix_organization_access_user ON reporting.organization_access(user_id); CREATE TABLE IF NOT EXISTS reporting.user_report_block_settings (user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE, report_code text NOT NULL, visible_blocks text[] NOT NULL DEFAULT ARRAY[]::text[], updated_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(user_id,report_code));""",connection);await command.ExecuteNonQueryAsync(ct);}
 public static async Task SetSettingsAsync(string departmentId,string? email,string roleLabel,string[] visibleReports,string[] visibleBlocks,NpgsqlDataSource ds,CancellationToken ct){
  const string sql="""INSERT INTO reporting.organization_access(department_id,role_label,visible_reports,visible_blocks,updated_at) VALUES(@id,@role,@reports,@blocks,now()) ON CONFLICT(department_id) DO UPDATE SET role_label=EXCLUDED.role_label,visible_reports=EXCLUDED.visible_reports,visible_blocks=EXCLUDED.visible_blocks,updated_at=now();""";
  roleLabel=NormalizeCommercialRoleLabel(roleLabel);
  if(roleLabel=="custom"){
   visibleReports=visibleReports.Where(code=>CommercialReportCatalog.Contains(code,StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
   visibleBlocks=visibleBlocks.Where(code=>CommercialBlockCatalog.Contains(code,StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  }else{
   visibleReports=ManagedReports;
   visibleBlocks=BlocksForCommercialRole(roleLabel);
  }
  await using var connection=await ds.OpenConnectionAsync(ct);
  await using var transaction=await connection.BeginTransactionAsync(ct);
  await using(var command=new NpgsqlCommand(sql,connection,transaction)){
   command.Parameters.AddWithValue("id",departmentId);
   command.Parameters.AddWithValue("role",roleLabel);
   command.Parameters.AddWithValue("reports",visibleReports);
   command.Parameters.AddWithValue("blocks",visibleBlocks);
   await command.ExecuteNonQueryAsync(ct);
  }
  if(!string.IsNullOrWhiteSpace(email)){
   Guid? userId=null;
   await using(var find=new NpgsqlCommand("SELECT id FROM auth.users WHERE lower(email)=lower(@email) AND deleted_at IS NULL;",connection,transaction)){
    find.Parameters.AddWithValue("email",email.Trim());
    userId=await find.ExecuteScalarAsync(ct) as Guid?;
   }
   if(userId is not null){
    await using(var blockCommand=new NpgsqlCommand("""INSERT INTO reporting.user_report_block_settings(user_id,report_code,visible_blocks,updated_at) VALUES(@userId,'informe_general_comercial',@blocks,now()) ON CONFLICT(user_id,report_code) DO UPDATE SET visible_blocks=EXCLUDED.visible_blocks,updated_at=now();""",connection,transaction)){
     blockCommand.Parameters.AddWithValue("userId",userId.Value);
     blockCommand.Parameters.AddWithValue("blocks",visibleBlocks);
     await blockCommand.ExecuteNonQueryAsync(ct);
    }
    await using(var clear=new NpgsqlCommand("DELETE FROM reporting.report_access ra USING reporting.report_definitions rd WHERE ra.report_definition_id=rd.id AND ra.user_id=@userId AND rd.code=ANY(@managedReports);",connection,transaction)){
     clear.Parameters.AddWithValue("userId",userId.Value);
     clear.Parameters.AddWithValue("managedReports",CommercialReportCatalog);
     await clear.ExecuteNonQueryAsync(ct);
    }
    await using(var grant=new NpgsqlCommand("""INSERT INTO reporting.report_access(report_definition_id,user_id,access_level) SELECT id,@userId,'viewer' FROM reporting.report_definitions WHERE code=ANY(@reports) ON CONFLICT(report_definition_id,user_id) WHERE user_id IS NOT NULL DO UPDATE SET access_level='viewer';""",connection,transaction)){
     grant.Parameters.AddWithValue("userId",userId.Value);
     grant.Parameters.AddWithValue("reports",visibleReports);
     await grant.ExecuteNonQueryAsync(ct);
    }
   }
  }
  await transaction.CommitAsync(ct);
 }
 public static async Task<(bool Configured,string[] Blocks)> GetUserBlockAccessAsync(Guid userId,string reportCode,NpgsqlDataSource ds,CancellationToken ct){await using var connection=await ds.OpenConnectionAsync(ct);await using var command=new NpgsqlCommand("SELECT visible_blocks FROM reporting.user_report_block_settings WHERE user_id=@userId AND report_code=@reportCode;",connection);command.Parameters.AddWithValue("userId",userId);command.Parameters.AddWithValue("reportCode",reportCode);var value=await command.ExecuteScalarAsync(ct);return value is string[] blocks?(true,blocks):(false,Array.Empty<string>());}
 public static async Task<object?> GetUserTeamScopeAsync(Guid userId,NpgsqlDataSource ds,CancellationToken ct){const string sql="""WITH RECURSIVE root AS (SELECT oa.department_id::bigint id,oa.role_label,d.name FROM reporting.organization_access oa JOIN bitrix.departments d ON d.id=oa.department_id::bigint LEFT JOIN bitrix.users head ON head.bitrix_id=d.head_bitrix_id LEFT JOIN auth.users panel_user ON lower(panel_user.email)=lower(head.email) WHERE oa.user_id=@userId OR panel_user.id=@userId ORDER BY CASE WHEN oa.user_id=@userId THEN 0 ELSE 1 END LIMIT 1), subtree AS (SELECT id,name FROM root UNION ALL SELECT d.id,d.name FROM bitrix.departments d JOIN subtree s ON d.parent_id=s.id), latest_users AS (SELECT DISTINCT ON (bitrix_id) bitrix_id,payload FROM bitrix.raw_payloads WHERE entity_type='user' ORDER BY bitrix_id,received_at DESC), members AS (SELECT DISTINCT COALESCE(NULLIF(TRIM(CONCAT_WS(' ',NULLIF(lu.payload->>'NAME',''),NULLIF(lu.payload->>'LAST_NAME',''))),''),NULLIF(u.full_name,''),u.bitrix_id) name FROM bitrix.users u JOIN latest_users lu ON lu.bitrix_id=u.bitrix_id WHERE jsonb_typeof(lu.payload->'UF_DEPARTMENT')='array' AND EXISTS (SELECT 1 FROM jsonb_array_elements_text(lu.payload->'UF_DEPARTMENT') value JOIN subtree s ON s.id=value::bigint)) SELECT (SELECT role_label FROM root),(SELECT name FROM root),COALESCE((SELECT array_agg(DISTINCT name ORDER BY name) FROM subtree),ARRAY[]::text[]),COALESCE((SELECT array_agg(DISTINCT name ORDER BY name) FROM members),ARRAY[]::text[]);""";await using var connection=await ds.OpenConnectionAsync(ct);await using var command=new NpgsqlCommand(sql,connection);command.Parameters.AddWithValue("userId",userId);await using var reader=await command.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct)||reader.IsDBNull(0))return null;return new{roleLabel=reader.GetString(0),departmentName=reader.GetString(1),departmentNames=reader.GetFieldValue<string[]>(2),memberNames=reader.GetFieldValue<string[]>(3)};}
 public static async Task<object> GetCommercialStructureAsync(NpgsqlDataSource ds,CancellationToken ct){
  var source=new List<DepartmentItem>();await using var connection=await ds.OpenConnectionAsync(ct);await using(var departmentCommand=new NpgsqlCommand("SELECT id::text,name,parent_id::text,head_bitrix_id FROM bitrix.departments ORDER BY sort_order,name;",connection)){await using var reader=await departmentCommand.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))source.Add(new DepartmentItem(reader.GetString(0),reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.IsDBNull(3)?null:reader.GetString(3)));}
  var ids=new HashSet<string>{"646"};var added=true;while(added){added=false;foreach(var x in source.Where(x=>x.ParentId is not null&&ids.Contains(x.ParentId)))added|=ids.Add(x.Id);}var commercial=source.Where(x=>ids.Contains(x.Id)).ToArray();
  var counts=new Dictionary<string,int>();var heads=new Dictionary<string,(string Name,string? Email)>();var localUsers=new Dictionary<string,Guid>(StringComparer.OrdinalIgnoreCase);var settings=new Dictionary<string,(string Role,string[] Reports,string[] Blocks)>();
  const string sql="""WITH latest AS (SELECT DISTINCT ON (bitrix_id) bitrix_id,payload FROM bitrix.raw_payloads WHERE entity_type='user' ORDER BY bitrix_id,received_at DESC), assigned AS (SELECT jsonb_array_elements_text(payload->'UF_DEPARTMENT') department_id FROM latest WHERE jsonb_typeof(payload->'UF_DEPARTMENT')='array') SELECT department_id,COUNT(*)::int FROM assigned WHERE department_id=ANY(@ids) GROUP BY department_id;""";
  await using(var cmd=new NpgsqlCommand(sql,connection)){cmd.Parameters.AddWithValue("ids",commercial.Select(x=>x.Id).ToArray());await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))counts[reader.GetString(0)]=reader.GetInt32(1);}
  await using(var cmd=new NpgsqlCommand("SELECT bitrix_id,full_name,email FROM bitrix.users WHERE bitrix_id=ANY(@ids);",connection)){cmd.Parameters.AddWithValue("ids",commercial.Where(x=>x.HeadId is not null).Select(x=>x.HeadId!).Distinct().ToArray());await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))heads[reader.GetString(0)]=(reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2));}
  await using(var cmd=new NpgsqlCommand("SELECT lower(email),id FROM auth.users WHERE deleted_at IS NULL;",connection)){await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))localUsers[reader.GetString(0)]=reader.GetGuid(1);}
  await using(var cmd=new NpgsqlCommand("SELECT department_id,role_label,visible_reports,visible_blocks FROM reporting.organization_access WHERE department_id=ANY(@ids);",connection)){cmd.Parameters.AddWithValue("ids",commercial.Select(x=>x.Id).ToArray());await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))settings[reader.GetString(0)]=(reader.GetString(1),reader.GetFieldValue<string[]>(2),reader.GetFieldValue<string[]>(3));}
  return new{departments=commercial.Select(x=>{var headName=x.HeadId is not null&&heads.TryGetValue(x.HeadId,out var h)?h.Name:null;var headEmail=x.HeadId is not null&&heads.TryGetValue(x.HeadId,out h)?h.Email:null;var localUserId=Guid.Empty;var userExists=!string.IsNullOrWhiteSpace(headEmail)&&localUsers.TryGetValue(headEmail,out localUserId);return new{id=x.Id,name=x.Name,parentId=x.ParentId,headId=x.HeadId,headName,headEmail,directUsers=counts.GetValueOrDefault(x.Id),roleLabel=settings.TryGetValue(x.Id,out var s)?s.Role:"viewer",visibleReports=settings.TryGetValue(x.Id,out s)?s.Reports:Array.Empty<string>(),visibleBlocks=settings.TryGetValue(x.Id,out s)?s.Blocks:Array.Empty<string>(),userExists,localUserId=userExists?localUserId:(Guid?)null};})};
 }
}
