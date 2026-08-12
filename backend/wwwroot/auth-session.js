const sessionMenu = document.querySelector(".sidebar .menu");
if (sessionMenu) {
  sessionMenu.style.visibility = "hidden";
  const currentPath = location.pathname;
  const currentReport = new URLSearchParams(location.search).get("id") ?? "";
  const isCurrent = (path, report = "") => currentPath === path && (!report || currentReport === report);
  const icons = {
    home: '<svg viewBox="0 0 24 24"><path d="m3 10 9-7 9 7v10a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1Z"/></svg>',
    reports: '<svg viewBox="0 0 24 24"><rect x="3" y="3" width="7" height="7" rx="2"/><rect x="14" y="3" width="7" height="7" rx="2"/><rect x="3" y="14" width="7" height="7" rx="2"/><rect x="14" y="14" width="7" height="7" rx="2"/></svg>',
    commercial: '<svg viewBox="0 0 24 24"><path d="M4 18 10 12l4 4 6-8"/><path d="M15 8h5v5"/></svg>',
    operative: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .34 1.88l.06.06-2.83 2.83-.06-.06a1.7 1.7 0 0 0-1.88-.34 1.7 1.7 0 0 0-1.03 1.56V21h-4v-.08A1.7 1.7 0 0 0 8.95 19.4a1.7 1.7 0 0 0-1.88.34l-.06.06-2.83-2.83.06-.06A1.7 1.7 0 0 0 4.58 15 1.7 1.7 0 0 0 3 14H3v-4h.08A1.7 1.7 0 0 0 4.6 8.95a1.7 1.7 0 0 0-.34-1.88l-.06-.06 2.83-2.83.06.06A1.7 1.7 0 0 0 8.97 4.6 1.7 1.7 0 0 0 10 3.08V3h4v.08a1.7 1.7 0 0 0 1.05 1.52 1.7 1.7 0 0 0 1.88-.34l.06-.06 2.83 2.83-.06.06A1.7 1.7 0 0 0 19.4 8.97 1.7 1.7 0 0 0 20.92 10H21v4h-.08A1.7 1.7 0 0 0 19.4 15Z"/></svg>',
    pnnc: '<svg viewBox="0 0 24 24"><path d="M12 3 21 8l-9 5-9-5Z"/><path d="m3 12 9 5 9-5"/><path d="m3 16 9 5 9-5"/></svg>',
    admin: '<svg viewBox="0 0 24 24"><path d="M12 3 4 7v5c0 5 3.4 8.7 8 10 4.6-1.3 8-5 8-10V7Z"/><path d="M9 12h6M12 9v6"/></svg>',
    sync: '<svg viewBox="0 0 24 24"><path d="M20 7h-5V2"/><path d="M20 7a8 8 0 0 0-14-2M4 17h5v5"/><path d="M4 17a8 8 0 0 0 14 2"/></svg>',
    users: '<svg viewBox="0 0 24 24"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/></svg>',
    structure: '<svg viewBox="0 0 24 24"><rect x="9" y="3" width="6" height="5" rx="1"/><rect x="3" y="16" width="6" height="5" rx="1"/><rect x="15" y="16" width="6" height="5" rx="1"/><path d="M12 8v4M6 16v-4h12v4"/></svg>',
    report: '<svg viewBox="0 0 24 24"><path d="M5 12h14M5 6h14M5 18h8"/></svg>',
    logout: '<svg viewBox="0 0 24 24"><path d="M10 17l5-5-5-5M15 12H3"/><path d="M14 3h5a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-5"/></svg>'
  };
  const icon = (name) => `<span class="nav-svg">${icons[name]}</span>`;
  const navLink = (href, iconName, label, report = "") => `<a href="${href}" target="_self" ${report ? `data-report-code="${report}"` : ""} class="sidebar-sub-link ${isCurrent(href.split("?")[0], report) ? "active" : ""}">${icon(iconName)}<span>${label}</span></a>`;
  sessionMenu.innerHTML = `
    <p>GENERAL</p>
    ${navLink("/", "home", "Inicio")}
    ${navLink("/informes.html", "reports", "Todos los informes")}
    <p>ÁREAS E INFORMES</p>
    <div class="sidebar-group" data-nav-group="comercial">
      <button class="sidebar-group-toggle" type="button" aria-expanded="false"><span class="sidebar-group-icon commercial">${icons.commercial}</span><span>Comercial</span><b>⌄</b></button>
      <div class="sidebar-group-items">
        ${navLink("/reporte.html?id=informe_general_comercial", "report", "Informe general", "informe_general_comercial")}
      </div>
    </div>
    <div class="sidebar-group" data-nav-group="operativa">
      <button class="sidebar-group-toggle" type="button" aria-expanded="false"><span class="sidebar-group-icon operative">${icons.operative}</span><span>Operativo</span><b>⌄</b></button>
      <div class="sidebar-group-items">
        ${navLink("/reporte.html?id=pnnc_operativa", "report", "PNNC Operativa", "pnnc_operativa")}
        ${navLink("/reporte.html?id=rch_operativa", "report", "RCH Operativa", "rch_operativa")}
        <span class="sidebar-coming">${icon("report")}<span>LP Operativa</span></span>
      </div>
    </div>
    <div class="sidebar-group sidebar-area-empty" data-nav-group="administrativo">
      <button class="sidebar-group-toggle" type="button" aria-expanded="false"><span class="sidebar-group-icon pnnc">${icons.pnnc}</span><span>Administrativo</span><b>⌄</b></button>
      <div class="sidebar-group-items"><span class="sidebar-empty">Sin informes configurados</span></div>
    </div>
    <div class="sidebar-group" data-nav-group="gerencia">
      <button class="sidebar-group-toggle" type="button" aria-expanded="false"><span class="sidebar-group-icon pnnc">${icons.pnnc}</span><span>Gerencia</span><b>⌄</b></button>
      <div class="sidebar-group-items">
        ${navLink("/reporte.html?id=informe_gerencia_2026_2027", "report", "Informe Gerencia", "informe_gerencia_2026_2027")}
      </div>
    </div>
    <div class="sidebar-group sidebar-area-empty" data-nav-group="subgerencia">
      <button class="sidebar-group-toggle" type="button" aria-expanded="false"><span class="sidebar-group-icon pnnc">${icons.pnnc}</span><span>Subgerencia</span><b>⌄</b></button>
      <div class="sidebar-group-items"><span class="sidebar-empty">Sin informes configurados</span></div>
    </div>
    <div class="sidebar-group" data-nav-group="administracion">
      <button class="sidebar-group-toggle" type="button" aria-expanded="false"><span class="sidebar-group-icon admin">${icons.admin}</span><span>Gestión</span><b>⌄</b></button>
      <div class="sidebar-group-items">
        <span data-required-permission="bitrix.sync.view">${navLink("/sincronizacion.html", "sync", "Sincronización Bitrix")}</span>
        <span data-superadmin-only>${navLink("/usuarios.html", "users", "Usuarios y roles")}</span>
        <span data-superadmin-only>${navLink("/estructura-comercial.html", "structure", "Estructura Comercial")}</span>
      </div>
    </div>`;

  sessionMenu.querySelectorAll(".sidebar-group-items > span[data-required-permission], .sidebar-group-items > span[data-superadmin-only]").forEach((row) => {
    row.style.cssText = "display:block;flex:0 0 auto;width:100%;height:auto;min-height:36px;margin:0";
    const link = row.querySelector(".sidebar-sub-link");
    if (link) link.style.cssText = "width:100%;height:auto;min-height:36px;margin:0";
  });

  document.querySelectorAll(".sidebar-group").forEach((group) => {
    const key = `navGroup:${group.dataset.navGroup}`;
    const hasActive = Boolean(group.querySelector("a.active"));
    const open = hasActive || localStorage.getItem(key) === "open";
    group.classList.toggle("open", open);
    group.querySelector("button").setAttribute("aria-expanded", String(open));
  });
  sessionMenu.addEventListener("click", (event) => {
    const reportLink = event.target.closest("a[data-report-code]");
    if (reportLink) {
      event.preventDefault();
      window.location.assign(reportLink.href);
      return;
    }
    const toggle = event.target.closest(".sidebar-group-toggle");
    if (!toggle) return;
    const group = toggle.closest(".sidebar-group");
    const open = !group.classList.contains("open");
    group.classList.toggle("open", open);
    toggle.setAttribute("aria-expanded", String(open));
    localStorage.setItem(`navGroup:${group.dataset.navGroup}`, open ? "open" : "closed");
  });

  const logoutButton = document.createElement("button");
  logoutButton.className = "sidebar-logout";
  logoutButton.type = "button";
  logoutButton.innerHTML = `${icon("logout")}<span>Cerrar sesión</span>`;
  logoutButton.addEventListener("click", async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    sessionStorage.removeItem("adminAccessKey");
    location.href = "/login.html";
  });
  sessionMenu.append(logoutButton);

  fetch("/api/auth/me").then((response) => response.ok ? response.json() : Promise.reject()).then((session) => {
    const allowed = new Set(session.accessibleReportCodes ?? []);
    const permissions = new Set(session.permissions ?? []);
    sessionMenu.querySelectorAll("[data-superadmin-only]").forEach((item) => {
      if (!session.isSuperAdmin) item.remove();
    });
    sessionMenu.querySelectorAll("[data-report-code]").forEach((link) => {
      if (session.roleCode !== "admin" && !allowed.has(link.dataset.reportCode)) link.remove();
    });
    sessionMenu.querySelectorAll("[data-required-permission]").forEach((item) => {
      const required = item.dataset.requiredPermission.split(" ");
      if (session.roleCode !== "admin" && !required.some((code) => permissions.has(code))) item.remove();
    });
    if (session.roleCode !== "admin") {
      sessionMenu.querySelectorAll(".sidebar-coming, .sidebar-area-empty").forEach((item) => item.remove());
    }
    sessionMenu.querySelectorAll(".sidebar-group").forEach((group) => {
      if (!group.querySelector(".sidebar-group-items a, .sidebar-coming, .sidebar-empty")) group.remove();
    });
  }).catch(() => {}).finally(() => {
    sessionMenu.style.visibility = "visible";
  });
}
