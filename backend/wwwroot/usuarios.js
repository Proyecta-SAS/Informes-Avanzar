let adminKey = sessionStorage.getItem("adminAccessKey") ?? "";
let accessData = null;

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
  const users = accessData.users.filter((user) => `${user.fullName} ${user.email}`.toLocaleLowerCase("es-CO").includes(normalized));
  document.getElementById("accessUserRows").innerHTML = users.map((user) => `
    <tr>
      <td><div class="access-person"><span>${user.fullName.charAt(0).toUpperCase()}</span><strong>${user.fullName}</strong></div></td>
      <td>${user.email}</td>
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

const renderWorkspace = () => {
  document.getElementById("newUserRole").innerHTML = roleOptions();
  renderUsers();
  renderPermissionMatrix();
  renderReportMatrix();
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
  await api("/api/admin/users", { method: "POST", body: JSON.stringify({ fullName: document.getElementById("newUserName").value, email: document.getElementById("newUserEmail").value, password: document.getElementById("newUserPassword").value, roleId: document.getElementById("newUserRole").value || null }) });
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

loadAccess().catch((exception) => {
  if (exception.message.includes("401")) location.href = "/login.html?returnUrl=/usuarios.html";
});
