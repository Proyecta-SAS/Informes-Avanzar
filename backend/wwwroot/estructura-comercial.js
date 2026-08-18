const escOrg = (value = "") => String(value).replace(/[&<>'"]/g, (character) => ({
  "&": "&amp;",
  "<": "&lt;",
  ">": "&gt;",
  "'": "&#39;",
  '"': "&quot;"
})[character]);

const initialsOrg = (name = "") =>
  name.split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join("").toUpperCase() || "AV";

const commercialRoles = [
  ["coordinator_rch", "Coordinador RCH", "Coordina equipos y lideres de la linea RCH."],
  ["leader_rch", "Lider RCH", "Gestiona un equipo comercial RCH."],
  ["coordinator_pnnc", "Coordinador Insolvencia PNNC", "Coordina equipos y lideres de la linea Insolvencia PNNC."],
  ["leader_pnnc", "Lider Insolvencia PNNC", "Gestiona un equipo comercial Insolvencia PNNC."],
  ["custom", "Personalizado", "Permite elegir manualmente paneles y tablas visibles."]
];

const commercialRoleGroups = [
  ["Linea RCH", ["coordinator_rch", "leader_rch"]],
  ["Linea Insolvencia PNNC", ["coordinator_pnnc", "leader_pnnc"]],
  ["Otros", ["custom"]]
];

const roleNames = Object.fromEntries(commercialRoles.map(([code, name]) => [code, name]));
const legacyRoleNames = {
  coordinator: "Coordinador",
  leader: "Lider",
  advisor: "Asesor",
  viewer: "Consulta"
};

const reportOptions = [
  ["informe_general_comercial", "Informe general"]
];

const generalBlockGroups = [
  ["Radicacion", [
    ["radicated_values", "Valores radicados por asesor"],
    ["advisor_negotiations", "Total negociaciones por asesor"],
    ["coordinator_values", "Valores radicados por coordinador"],
    ["leader_values", "Valores radicados por lider"],
    ["coordinator_detail", "Detalle coordinadores"],
    ["leader_detail", "Radicaciones por lideres"]
  ]],
  ["Comisiones", [
    ["advisor_commissions", "Comisiones por asesor"]
  ]],
  ["Carteras", [
    ["portfolio_state", "Estado de cartera"],
    ["portfolio_collected", "Cartera recaudada"]
  ]],
  ["Embudos", [
    ["funnel_insolvency", "Embudo Insolvencia"],
    ["funnel_rch", "Embudo RCH"],
    ["commercial_possible_close_rch", "(COM) Posible Cierre RCH"],
    ["commercial_possible_close_pnnc", "(COM) Posible Cierre PNNC"]
  ]]
];

const allReportCodes = reportOptions.map(([code]) => code);
const allGeneralBlockCodes = generalBlockGroups.flatMap(([, items]) => items.map(([code]) => code));
const lineSpecificBlockCodes = new Set([
  "funnel_insolvency",
  "funnel_rch",
  "commercial_possible_close_rch",
  "commercial_possible_close_pnnc"
]);
const restrictedCommercialBlockCodes = new Set([
  "coordinator_values",
  "coordinator_detail",
  "leader_detail"
]);
const sharedGeneralBlockCodes = allGeneralBlockCodes.filter((code) =>
  !lineSpecificBlockCodes.has(code) && !restrictedCommercialBlockCodes.has(code)
);
const normalizeCommercialRole = (role) => roleNames[role] ? role : "leader_rch";
const getRoleName = (role) => roleNames[role] ?? legacyRoleNames[role] ?? "Lider RCH";
const blockCodesForRole = (role) => {
  const normalized = normalizeCommercialRole(role);
  if (normalized === "custom") return [];
  const isPnnc = normalized.endsWith("_pnnc");
  const sharedBlocks = normalized.startsWith("leader_")
    ? sharedGeneralBlockCodes.filter((code) => code !== "leader_values")
    : sharedGeneralBlockCodes;
  return [
    ...sharedBlocks,
    isPnnc ? "funnel_insolvency" : "funnel_rch",
    isPnnc ? "commercial_possible_close_pnnc" : "commercial_possible_close_rch"
  ];
};

const applyPresetAccess = (settings) => {
  const role = normalizeCommercialRole(settings.querySelector(".organization-role")?.value);
  const isCustom = role === "custom";
  const blocks = new Set(blockCodesForRole(settings.querySelector(".organization-role")?.value));
  settings.querySelectorAll(".organization-report-check").forEach((input) => {
    input.disabled = !isCustom;
    if (!isCustom) input.checked = true;
  });
  settings.querySelectorAll(".organization-block-check").forEach((input) => {
    input.disabled = !isCustom;
    if (!isCustom) input.checked = blocks.has(input.value);
  });
};

const roleOptionsMarkup = (selectedRole) => {
  const normalized = normalizeCommercialRole(selectedRole);
  return commercialRoleGroups.map(([group, codes]) => `
    <optgroup label="${group}">
      ${codes.map((code) => `<option value="${code}" ${normalized === code ? "selected" : ""}>${roleNames[code]}</option>`).join("")}
    </optgroup>`).join("");
};

const orgCard = (department, children) => {
  const role = normalizeCommercialRole(department.roleLabel);
  const selectedReports = new Set(role === "custom" ? (department.visibleReports ?? []) : [...(department.visibleReports ?? []), ...allReportCodes]);
  const selectedBlocks = new Set(role === "custom" ? (department.visibleBlocks ?? []) : [...(department.visibleBlocks ?? []), ...blockCodesForRole(role)]);

  return `
    <article class="organization-card ${children.length ? "has-children" : ""}" data-department-id="${department.id}" data-head-name="${escOrg(department.headName ?? "")}" data-head-email="${escOrg(department.headEmail ?? "")}">
      <div class="organization-card-top">
        <div class="organization-card-type">${escOrg(department.name)}</div>
        <span class="organization-role-badge ${role}">${escOrg(getRoleName(role))}</span>
      </div>
      <div class="organization-person">
        <span>${initialsOrg(department.headName ?? department.name)}</span>
        <div>
          <strong>${escOrg(department.headName ?? "Responsable no especificado")}</strong>
          <small>${escOrg(department.headEmail ?? "Sin correo registrado")}</small>
        </div>
      </div>
      <div class="organization-card-metric"><small>Empleados</small><b>${department.directUsers} asignados</b></div>
      <div class="organization-card-actions">
        <button class="organization-manage" type="button">Gestionar acceso</button>
        <button class="organization-create-user ${department.userExists ? "is-created" : ""}" type="button" ${department.userExists || !department.headEmail ? "disabled" : ""}>${department.userExists ? "Usuario creado" : "Crear usuario"}</button>
      </div>
      <div class="organization-user-result" hidden></div>
      <div class="organization-settings" hidden>
        <label>Rol comercial predefinido<select class="organization-role">${roleOptionsMarkup(role)}</select></label>
        <fieldset>
          <legend>Paneles visibles incluidos</legend>
          ${reportOptions.map(([code, name]) => `<label><input class="organization-report-check" type="checkbox" value="${code}" ${selectedReports.has(code) ? "checked" : ""} ${role === "custom" ? "" : "disabled"}>${name}</label>`).join("")}
        </fieldset>
        <div class="organization-block-access">
          <div><strong>Tablas del Informe General incluidas</strong><span class="organization-preset-note">Predefinido</span></div>
          ${generalBlockGroups.map(([group, items]) => `
            <fieldset>
              <legend>${group}</legend>
              ${items.map(([code, name]) => `<label><input class="organization-block-check" type="checkbox" value="${code}" ${selectedBlocks.has(code) ? "checked" : ""} ${role === "custom" ? "" : "disabled"}>${name}</label>`).join("")}
            </fieldset>
          `).join("")}
        </div>
        <button class="organization-save" type="button">Guardar rol</button>
        <small class="organization-save-state">${role === "custom" ? "Selecciona manualmente los paneles y tablas visibles." : "Este rol usa permisos predefinidos. Lo eliminado o archivado no se incluye en el catalogo."}</small>
      </div>
      <footer>${children.length ? `<button class="organization-toggle" type="button" aria-expanded="false"><span>${children.length} departamentos</span><b>⌄</b></button>` : "Sin subdepartamentos"}</footer>
    </article>`;
};

const orgNode = (department, allDepartments, depth = 0) => {
  const children = allDepartments.filter((item) => item.parentId === department.id);
  return `
    <div class="organization-node ${children.length && depth > 0 ? "collapsed" : ""}">
      ${orgCard(department, children)}
      ${children.length ? `<div class="organization-children">${children.map((child) => orgNode(child, allDepartments, depth + 1)).join("")}</div>` : ""}
    </div>`;
};

const loadCommercialStructure = () => fetch("/api/organization/commercial")
  .then((response) => {
    if (!response.ok) throw Error("No fue posible consultar la estructura.");
    return response.json();
  })
  .then((data) => {
    const departments = data.departments ?? [];
    if (!departments.length) {
      organizationLoading.hidden = false;
      organizationLoading.innerHTML = `<strong>Estructura pendiente de sincronizar</strong><span>La base de datos todavia no tiene departamentos comerciales.</span><button id="organizationInitialSync" type="button">Sincronizar estructura desde Bitrix</button>`;
      return;
    }

    const root = departments.find((item) => item.id === "646");
    const lines = departments.filter((item) => item.parentId === root?.id);
    orgDepartments.textContent = Math.max(0, departments.length - 1);
    orgLeaders.textContent = departments.filter((item) => item.headName).length;
    orgUsers.textContent = departments.reduce((total, item) => total + item.directUsers, 0).toLocaleString("es-CO");
    organizationTrees.innerHTML = lines.map((line) => `
      <section class="organization-line">
        <div class="organization-line-heading"><span>Linea comercial</span><h2>${escOrg(line.name)}</h2></div>
        <div class="organization-chart-scroll"><div class="organization-chart">${orgNode(line, departments)}</div></div>
      </section>`).join("");
    organizationLoading.hidden = true;
  })
  .catch((error) => {
    organizationLoading.hidden = false;
    organizationLoading.textContent = error.message;
  });

const restoreOrganizationSettings = (settings) => {
  const origin = document.querySelector(`.organization-card[data-department-id="${settings.dataset.originDepartment}"]`);
  if (origin) origin.querySelector(".organization-user-result").after(settings);
};

const closeOrganizationSettings = () => {
  document.querySelectorAll("body>.organization-settings").forEach((settings) => {
    settings.hidden = true;
    restoreOrganizationSettings(settings);
  });
  document.querySelectorAll(".organization-manage").forEach((button) => {
    button.textContent = "Gestionar acceso";
  });
  document.body.classList.remove("organization-drawer-open");
  organizationSettingsBackdrop.hidden = true;
};

const getSettingsPayload = (card, settings) => {
  const role = normalizeCommercialRole(settings.querySelector(".organization-role").value);
  const isCustom = role === "custom";
  return {
    email: card.dataset.headEmail,
    roleLabel: role,
    visibleReports: isCustom ? [...settings.querySelectorAll(".organization-report-check:checked")].map((input) => input.value) : allReportCodes,
    visibleBlocks: isCustom ? [...settings.querySelectorAll(".organization-block-check:checked")].map((input) => input.value) : blockCodesForRole(role)
  };
};

const getActiveSettingsForCard = (card) =>
  document.querySelector(`.organization-settings[data-origin-department="${card.dataset.departmentId}"]`)
  ?? card.querySelector(".organization-settings");

const saveOrganizationSettings = async (card, settings, state, save) => {
  save.disabled = true;
  state.textContent = "Guardando configuracion...";
  const payload = getSettingsPayload(card, settings);
  const response = await fetch(`/api/organization/commercial/${card.dataset.departmentId}/settings`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  save.disabled = false;
  state.textContent = response.ok ? `Rol guardado: ${getRoleName(payload.roleLabel)} · ${payload.visibleBlocks.length} tablas visibles` : "No fue posible guardar";
  if (response.ok) {
    const badge = card.querySelector(".organization-role-badge");
    badge.className = `organization-role-badge ${payload.roleLabel}`;
    badge.textContent = getRoleName(payload.roleLabel);
    applyPresetAccess(settings);
  }
};

loadCommercialStructure();

organizationLoading.addEventListener("click", async (event) => {
  const button = event.target.closest("#organizationInitialSync");
  if (!button) return;
  button.disabled = true;
  button.textContent = "Sincronizando...";
  try {
    const response = await fetch("/api/bitrix/sync/departments", { method: "POST" });
    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw Error(error.error ?? "No fue posible sincronizar los departamentos.");
    }
    organizationLoading.innerHTML = "Consultando estructura comercial...";
    await loadCommercialStructure();
  } catch (error) {
    button.disabled = false;
    button.textContent = "Reintentar sincronizacion";
    organizationLoading.insertAdjacentHTML("beforeend", `<small>${escOrg(error.message)}</small>`);
  }
});

organizationLoading.insertAdjacentHTML("beforebegin", `
  <section class="organization-toolbar">
    <label><span>⌕</span><input id="organizationSearch" type="search" placeholder="Buscar coordinador, lider o equipo"></label>
    <button id="organizationExpand" type="button">Expandir todo</button>
    <button id="organizationCollapse" type="button">Contraer todo</button>
  </section>`);

document.body.insertAdjacentHTML("beforeend", `<div id="organizationSettingsBackdrop" class="organization-settings-backdrop" hidden></div>`);

organizationTrees.addEventListener("click", async (event) => {
  const toggle = event.target.closest(".organization-toggle");
  if (toggle) {
    const node = toggle.closest(".organization-node");
    const collapsed = node.classList.toggle("collapsed");
    toggle.setAttribute("aria-expanded", String(!collapsed));
    return;
  }

  const manage = event.target.closest(".organization-manage");
  if (manage) {
    const card = manage.closest(".organization-card");
    const settings = card.querySelector(".organization-settings");
    if (!settings.hidden) {
      closeOrganizationSettings();
      return;
    }
    closeOrganizationSettings();
    settings.hidden = false;
    settings.dataset.originDepartment = card.dataset.departmentId;
    applyPresetAccess(settings);
    if (!settings.querySelector(".organization-settings-header")) {
      settings.insertAdjacentHTML("afterbegin", `
        <div class="organization-settings-header">
          <div>
            <small>Configuracion de acceso</small>
            <strong>${escOrg(card.dataset.headName || card.querySelector(".organization-card-type").textContent)}</strong>
            <span>${escOrg(card.dataset.headEmail || "Sin correo registrado")}</span>
          </div>
          <button class="organization-settings-close" type="button" aria-label="Cerrar">×</button>
        </div>`);
    }
    document.body.append(settings);
    document.body.classList.add("organization-drawer-open");
    organizationSettingsBackdrop.hidden = false;
    manage.textContent = "Cerrar gestion";
    return;
  }

  const create = event.target.closest(".organization-create-user");
  if (create) {
    const card = create.closest(".organization-card");
    const result = card.querySelector(".organization-user-result");
    const settings = getActiveSettingsForCard(card);
    const payload = settings
      ? getSettingsPayload(card, settings)
      : { roleLabel: normalizeCommercialRole(card.querySelector(".organization-role-badge")?.classList[1]), visibleReports: allReportCodes, visibleBlocks: [] };
    create.disabled = true;
    create.textContent = "Creando...";
    const response = await fetch(`/api/admin/organization/${card.dataset.departmentId}/create-user`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ fullName: card.dataset.headName, email: card.dataset.headEmail, ...payload })
    });
    const data = await response.json().catch(() => ({}));
    create.textContent = response.ok ? "Usuario creado" : "Crear usuario";
    create.disabled = response.ok;
    result.hidden = false;
    result.innerHTML = response.ok
      ? `<b>Usuario creado</b><span>${escOrg(card.dataset.headEmail)}</span><label>Contrasena temporal<strong>${escOrg(data.temporaryPassword)}</strong></label><small>Guardala ahora. El usuario ya aparece en Usuarios y roles.</small>`
      : `<b>No fue posible crear</b><span>${escOrg(data.message ?? "Verifica que el correo no este registrado.")}</span>`;
  }
});

document.addEventListener("change", (event) => {
  if (!event.target.matches(".organization-role")) return;
  applyPresetAccess(event.target.closest(".organization-settings"));
});

document.addEventListener("click", async (event) => {
  if (event.target.closest(".organization-settings-close")) {
    closeOrganizationSettings();
    return;
  }

  const save = event.target.closest("body>.organization-settings .organization-save");
  if (!save) return;
  const settings = save.closest(".organization-settings");
  const card = document.querySelector(`.organization-card[data-department-id="${settings.dataset.originDepartment}"]`);
  const state = settings.querySelector(".organization-save-state");
  await saveOrganizationSettings(card, settings, state, save);
});

organizationSettingsBackdrop.addEventListener("click", closeOrganizationSettings);
document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") closeOrganizationSettings();
});

organizationSearch.addEventListener("input", (event) => {
  const query = event.target.value.trim().toLocaleLowerCase("es-CO");
  document.querySelectorAll(".organization-card").forEach((card) => {
    card.classList.toggle("search-hidden", query && !card.textContent.toLocaleLowerCase("es-CO").includes(query));
  });
});

organizationExpand.addEventListener("click", () => {
  document.querySelectorAll(".organization-node").forEach((node) => {
    node.classList.remove("collapsed");
    node.querySelector(":scope > .organization-card .organization-toggle")?.setAttribute("aria-expanded", "true");
  });
});

organizationCollapse.addEventListener("click", () => {
  document.querySelectorAll(".organization-node:has(.organization-children)").forEach((node) => {
    node.classList.add("collapsed");
    node.querySelector(":scope > .organization-card .organization-toggle")?.setAttribute("aria-expanded", "false");
  });
});

organizationTrees.insertAdjacentHTML("afterend", `
  <section class="organization-help">
    <div class="organization-help-heading">
      <span class="section-kicker">Guia de acceso</span>
      <h2>Roles de estructura comercial</h2>
      <p>Selecciona el rol segun la linea y el nivel del responsable. RCH e Insolvencia PNNC tienen coordinador y lider separados; Personalizado permite escoger manualmente.</p>
    </div>
    <div class="organization-role-guide">
      ${commercialRoles.map(([code, name, description], index) => `
        <article class="${code}">
          <span>${String(index + 1).padStart(2, "0")}</span>
          <div><h3>${name}</h3><p>${description}</p></div>
        </article>`).join("")}
    </div>
    <div class="organization-permission-steps">
      <h3>Como asignar los permisos</h3>
      <ol>
        <li><b>Ubica el equipo</b><span>Busca el coordinador, lider o departamento en el organigrama.</span></li>
        <li><b>Abre Gestionar acceso</b><span>Despliega la configuracion dentro de la tarjeta.</span></li>
        <li><b>Selecciona el rol</b><span>Elige la linea correcta: RCH o Insolvencia PNNC, y luego coordinador o lider.</span></li>
        <li><b>Guarda</b><span>La configuracion queda registrada con todos los paneles vigentes.</span></li>
        <li><b>Crea usuario</b><span>Si falta la cuenta, usa el boton Crear usuario para generar acceso.</span></li>
      </ol>
      <div class="organization-permission-note"><b>Importante:</b> usa Personalizado solo cuando el responsable necesita una combinacion distinta de tablas.</div>
    </div>
  </section>`);
