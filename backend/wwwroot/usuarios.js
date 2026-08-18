let adminKey = sessionStorage.getItem("adminAccessKey") ?? "";
let accessData = null;
let selectedCommercialRole = "viewer";
const commercialManagedReportCodes = [
  "informe_general_comercial"
];
const generalBlockGroups = [
  ["Radicación", [["radicated_values", "Valores radicados por asesor"], ["advisor_negotiations", "Total negociaciones por asesor"], ["coordinator_values", "Valores radicados por coordinador"], ["leader_values", "Valores radicados por líder"], ["coordinator_detail", "Detalle coordinadores"], ["leader_detail", "Radicaciones por líderes"]]],
  ["Comisiones", [["advisor_commissions", "Comisiones por asesor"]]],
  ["Carteras", [["portfolio_state", "Estado de cartera 2025"], ["portfolio_collected", "Cartera recaudada"]]],
  ["Embudos", [["funnel_insolvency", "Embudo Insolvencia"], ["funnel_rch", "Embudo RCH"], ["commercial_possible_close_rch", "(COM) Posible Cierre RCH"], ["commercial_possible_close_pnnc", "(COM) Posible Cierre PNNC"]]]
];
const allGeneralBlockCodes = generalBlockGroups.flatMap(([, items]) => items.map(([code]) => code));
const lineSpecificGeneralBlockCodes = new Set(["funnel_insolvency", "funnel_rch", "commercial_possible_close_rch", "commercial_possible_close_pnnc"]);
const commercialRestrictedBlockCodes = new Set(["coordinator_values", "coordinator_detail", "leader_detail"]);
const leaderExcludedGeneralBlockCodes = new Set(["coordinator_values", "coordinator_detail", "leader_values"]);
const leaderGeneralBlockCodes = allGeneralBlockCodes.filter((code) => !leaderExcludedGeneralBlockCodes.has(code));
const generalDefaultBlockCodes = allGeneralBlockCodes.filter((code) =>
  !lineSpecificGeneralBlockCodes.has(code) && !commercialRestrictedBlockCodes.has(code)
);

const api = async (url, options = {}) => {
  const response = await fetch(url, {
    ...options,
    headers: { "Content-Type": "application/json", "X-Admin-Key": adminKey, ...(options.headers ?? {}) }
  });
  if (!response.ok) {
    const detail = await response.json().catch(() => ({}));
    throw new Error(detail.detail ?? detail.message ?? (response.status === 401 ? "Clave administrativa incorrecta." : `HTTP ${response.status}`));
  }
  return response.status === 204 ? null : response.json();
};

const roleOptions = (selectedId = "") => `<option value="">Sin rol</option>${accessData.roles.map((role) => `<option value="${role.id}" ${role.id === selectedId ? "selected" : ""}>${role.name}</option>`).join("")}`;

const renderUsers = (query = "") => {
  const normalized = query.trim().toLocaleLowerCase("es-CO");
  const users = accessData.users.filter((user) => `${user.fullName} ${user.email} ${user.bitrixUserId ?? ""}`.toLocaleLowerCase("es-CO").includes(normalized));
  document.getElementById("accessUserRows").innerHTML = users.map((user) => `
    <tr>
      <td><div class="access-person"><span>${user.fullName.charAt(0).toUpperCase()}</span><strong>${user.fullName}</strong></div></td>
      <td>${user.email}</td>
      <td><code>${user.bitrixUserId ?? "Sin ID"}</code></td>
      <td><em class="user-status ${user.status}">${user.status === "active" ? "Activo" : user.status}</em></td>
      <td><select class="user-role-select" data-user-id="${user.id}">${roleOptions(user.roleIds[0] ?? "")}</select></td>
      <td><div class="user-actions"><button class="button-secondary edit-user" data-user-id="${user.id}" type="button">Editar</button><button class="button-danger delete-user" data-user-id="${user.id}" type="button">Eliminar</button></div></td>
    </tr>`).join("");
};

const toggle = (checked, attributes) => `<label class="access-toggle"><input type="checkbox" ${checked ? "checked" : ""} ${attributes}><span></span></label>`;

const renderPermissionMatrix = () => {
  document.getElementById("permissionMatrix").innerHTML = `
    <div class="permission-grid" style="--access-columns:${accessData.roles.length}">
      <div class="permission-corner">Permiso</div>${accessData.roles.map((role) => `<div class="permission-role"><strong>${role.name}</strong><small>${role.description ?? "Rol del sistema"}</small></div>`).join("")}
      ${accessData.permissions.map((permission) => `<div class="permission-name"><strong>${permission.name}</strong><small>${permission.description ?? permission.code}</small></div>${accessData.roles.map((role) => `<div class="permission-cell">${toggle(role.permissionIds.includes(permission.id), `data-role-id="${role.id}" data-permission-id="${permission.id}"`)}</div>`).join("")}`).join("")}
    </div>`;
};

const renderReportMatrix = () => {
  document.getElementById("reportAccessMatrix").innerHTML = `
    <div class="permission-grid" style="--access-columns:${accessData.roles.length}">
      <div class="permission-corner">Informe</div>${accessData.roles.map((role) => `<div class="permission-role"><strong>${role.name}</strong><small>Acceso por rol</small></div>`).join("")}
      ${accessData.reports.map((report) => `<div class="permission-name"><strong>${report.name}</strong><small>${report.code}</small></div>${accessData.roles.map((role) => `<div class="permission-cell">${toggle(Boolean(report.roleAccess?.[role.id]), `data-report-id="${report.id}" data-report-role-id="${role.id}"`)}</div>`).join("")}`).join("")}
    </div>`;
};

const renderCommercialAccess = () => {
  const commercialReports = accessData.reports.filter((report) => commercialManagedReportCodes.includes(report.code));
  document.getElementById("commercialAccessMatrix").innerHTML = `
    <div class="commercial-access-grid" style="--commercial-report-count:${commercialReports.length}">
      <div class="commercial-grid-head user-column">Comercial</div>
      <div class="commercial-grid-head role-column">Rol asignado</div>
      ${commercialReports.map((report) => `<div class="commercial-grid-head"><strong>${report.name}</strong><small>Acceso individual</small></div>`).join("")}
      ${accessData.users.map((user) => `
        <div class="commercial-user-cell"><span>${user.fullName.charAt(0).toUpperCase()}</span><div><strong>${user.fullName}</strong><small>${user.email}</small></div></div>
        <div class="commercial-role-cell"><select class="commercial-role-select" data-user-id="${user.id}">${roleOptions(user.roleIds[0] ?? "")}</select></div>
        ${commercialReports.map((report) => `<div class="commercial-permission-cell">${toggle(Boolean(report.userAccess?.[user.id]), `data-commercial-report-id="${report.id}" data-commercial-user-id="${user.id}"`)}</div>`).join("")}
      `).join("")}
    </div>`;
};

const renderGeneralBlockAssignment = () => {
  const blockAssignment = document.getElementById("generalBlockAssignment");
  blockAssignment.querySelectorAll(".role-assignment-block-group").forEach((group) => group.remove());
  blockAssignment.insertAdjacentHTML("beforeend", generalBlockGroups.map(([group, items]) => `
    <div class="role-assignment-block-group">
      <strong>${group}</strong>
      ${items.map(([code, name]) => `<label><input class="general-block-check" type="checkbox" value="${code}"> ${name}</label>`).join("")}
    </div>`).join(""));
};

const renderRoleAssignmentReports = () => {
  const fieldset = document.getElementById("roleAssignmentReports");
  const commercialReports = accessData.reports.filter((report) => commercialManagedReportCodes.includes(report.code));
  fieldset.innerHTML = `<legend>2. Marca los paneles visibles</legend>${commercialReports.map((report) => `<label><input type="checkbox" value="${report.code}"> ${report.name}</label>`).join("")}`;
};

const applyCommercialRoleDefaults = () => {
  const form = document.getElementById("roleAssignmentForm");
  if (!["coordinator", "leader"].includes(selectedCommercialRole)) return;
  commercialManagedReportCodes.forEach((code) => {
    const input = form.querySelector(`input[value="${code}"]`);
    if (input) input.checked = true;
  });
  const defaultBlocks = selectedCommercialRole === "coordinator"
    ? new Set(generalDefaultBlockCodes)
    : new Set(leaderGeneralBlockCodes.filter((code) => !lineSpecificGeneralBlockCodes.has(code) && !commercialRestrictedBlockCodes.has(code)));
  form.querySelectorAll(".general-block-check").forEach((input) => input.checked = defaultBlocks.has(input.value));
};

const renderWorkspace = () => {
  document.getElementById("accessUserCount").textContent = accessData.users.length.toLocaleString("es-CO");
  document.getElementById("accessRoleCount").textContent = accessData.roles.length.toLocaleString("es-CO");
  document.getElementById("accessReportCount").textContent = accessData.reports.length.toLocaleString("es-CO");
  document.getElementById("newUserRole").innerHTML = roleOptions();
  renderUsers();
  renderPermissionMatrix();
  renderReportMatrix();
  renderCommercialAccess();
  renderRoleAssignmentReports();
  renderGeneralBlockAssignment();
  applyCommercialRoleDefaults();
  document.getElementById("roleAssignmentUser").innerHTML = `<option value="">Seleccionar usuario…</option>${accessData.users.map((user) => `<option value="${user.id}">${user.fullName} · ${user.email}</option>`).join("")}`;
};

const loadAccess = async () => {
  accessData = await api("/api/admin/access-management");
  renderWorkspace();
};

document.getElementById("showCreateUser").addEventListener("click", () => document.getElementById("createUserPanel").hidden = false);
document.getElementById("cancelCreateUser").addEventListener("click", () => document.getElementById("createUserPanel").hidden = true);
document.getElementById("userSearch").addEventListener("input", (event) => renderUsers(event.target.value));

document.getElementById("createUserForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  await api("/api/admin/users", { method: "POST", body: JSON.stringify({ fullName: document.getElementById("newUserName").value, email: document.getElementById("newUserEmail").value, bitrixUserId: document.getElementById("newUserBitrixId").value, password: document.getElementById("newUserPassword").value, roleId: document.getElementById("newUserRole").value || null }) });
  event.target.reset();
  document.getElementById("createUserPanel").hidden = true;
  await loadAccess();
});

document.getElementById("accessUserRows").addEventListener("click", async (event) => {
  const userId = event.target.dataset.userId;
  if (event.target.matches(".edit-user")) {
    const user = accessData.users.find((item) => item.id === userId);
    document.getElementById("editUserId").value = user.id;
    document.getElementById("editUserName").value = user.fullName;
    document.getElementById("editUserEmail").value = user.email;
    document.getElementById("editUserBitrixId").value = user.bitrixUserId ?? "";
    document.getElementById("editUserStatus").value = user.status;
    document.getElementById("editUserRole").innerHTML = roleOptions(user.roleIds[0] ?? "");
    document.getElementById("editUserPassword").value = "";
    document.getElementById("editUserPanel").hidden = false;
    document.getElementById("editUserPanel").scrollIntoView({ behavior: "smooth" });
  }
  if (event.target.matches(".delete-user") && confirm("¿Eliminar este usuario? Esta acción desactivará su acceso.")) {
    await api(`/api/admin/users/${userId}`, { method: "DELETE" });
    await loadAccess();
  }
});

document.getElementById("cancelEditUser").addEventListener("click", () => document.getElementById("editUserPanel").hidden = true);
document.getElementById("editUserForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const userId = document.getElementById("editUserId").value;
  await api(`/api/admin/users/${userId}`, { method: "PUT", body: JSON.stringify({
    fullName: document.getElementById("editUserName").value,
    email: document.getElementById("editUserEmail").value,
    bitrixUserId: document.getElementById("editUserBitrixId").value,
    status: document.getElementById("editUserStatus").value,
    roleId: document.getElementById("editUserRole").value || null
  }) });
  const password = document.getElementById("editUserPassword").value;
  if (password) await api(`/api/admin/users/${userId}/password`, { method: "PUT", body: JSON.stringify({ password }) });
  document.getElementById("editUserPanel").hidden = true;
  await loadAccess();
});

document.getElementById("accessUserRows").addEventListener("change", async (event) => {
  if (!event.target.matches(".user-role-select")) return;
  await api(`/api/admin/users/${event.target.dataset.userId}/role`, { method: "PUT", body: JSON.stringify({ roleId: event.target.value || null }) });
});
document.getElementById("permissionMatrix").addEventListener("change", async (event) => {
  if (!event.target.matches("[data-permission-id]")) return;
  await api(`/api/admin/roles/${event.target.dataset.roleId}/permissions/${event.target.dataset.permissionId}`, { method: "PUT", body: JSON.stringify({ enabled: event.target.checked }) });
});
document.getElementById("reportAccessMatrix").addEventListener("change", async (event) => {
  if (!event.target.matches("[data-report-id]")) return;
  await api(`/api/admin/reports/${event.target.dataset.reportId}/roles/${event.target.dataset.reportRoleId}`, { method: "PUT", body: JSON.stringify({ enabled: event.target.checked, accessLevel: "viewer" }) });
});
document.getElementById("commercialAccessMatrix").addEventListener("change", async (event) => {
  if (event.target.matches(".commercial-role-select")) {
    await api(`/api/admin/users/${event.target.dataset.userId}/role`, { method: "PUT", body: JSON.stringify({ roleId: event.target.value || null }) });
    await loadAccess();
    return;
  }
  if (event.target.matches("[data-commercial-report-id]")) {
    await api(`/api/admin/reports/${event.target.dataset.commercialReportId}/users/${event.target.dataset.commercialUserId}`, { method: "PUT", body: JSON.stringify({ enabled: event.target.checked, accessLevel: "viewer" }) });
  }
});
document.getElementById("userRoleCards").addEventListener("click", (event) => {
  const card = event.target.closest("[data-role-label]");
  if (!card) return;
  selectedCommercialRole = card.dataset.roleLabel;
  document.querySelectorAll("#userRoleCards [data-role-label]").forEach((item) => item.classList.toggle("selected", item === card));
  document.getElementById("selectedRoleName").textContent = card.querySelector("h3").textContent;
  applyCommercialRoleDefaults();
});
document.getElementById("roleAssignmentForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const userId = document.getElementById("roleAssignmentUser").value;
  const state = document.getElementById("roleAssignmentState");
  if (!userId) { state.textContent = "Selecciona un usuario."; return; }
  const systemRoleCode = ["director", "coordinator", "leader"].includes(selectedCommercialRole) ? "report_manager" : "report_viewer";
  const systemRole = accessData.roles.find((role) => role.code === systemRoleCode);
  const enabledReports = new Set([...event.target.querySelectorAll('input[type="checkbox"]:checked')].map((input) => input.value));
  const commercialReports = accessData.reports.filter((report) => commercialManagedReportCodes.includes(report.code));
  const visibleBlocks = [...event.target.querySelectorAll(".general-block-check:checked")].map((input) => input.value);
  state.textContent = "Aplicando configuración…";
  await api(`/api/admin/users/${userId}/role`, { method: "PUT", body: JSON.stringify({ roleId: systemRole?.id ?? null }) });
  await Promise.all(commercialReports.map((report) => api(`/api/admin/reports/${report.id}/users/${userId}`, { method: "PUT", body: JSON.stringify({ enabled: enabledReports.has(report.code), accessLevel: "viewer" }) })));
  await api(`/api/admin/reports/informe-general/users/${userId}/blocks`, { method: "PUT", body: JSON.stringify({ visibleBlocks }) });
  state.textContent = "Rol y permisos guardados correctamente.";
  await loadAccess();
});

loadAccess().catch((exception) => {
  if (exception.message.includes("401")) location.href = "/login.html?returnUrl=/usuarios.html";
});
