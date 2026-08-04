const sessionMenu = document.querySelector(".sidebar .menu");
if (sessionMenu) {
  const currentPath = location.pathname;
  const currentReport = new URLSearchParams(location.search).get("id") ?? "";
  const isCurrent = (path, report = "") => currentPath === path && (!report || currentReport === report);
  const navLink = (href, icon, label, report = "") => `<a href="${href}" class="sidebar-sub-link ${isCurrent(href.split("?")[0], report) ? "active" : ""}"><span>${icon}</span><span>${label}</span></a>`;
  sessionMenu.innerHTML = `
    <p>GENERAL</p>
    ${navLink("/", "⌂", "Inicio")}
    ${navLink("/informes.html", "▦", "Todos los informes")}
    <p>ÁREAS E INFORMES</p>
    <div class="sidebar-group" data-nav-group="comercial">
      <button class="sidebar-group-toggle" type="button" aria-expanded="false"><span class="sidebar-group-icon commercial">↗</span><span>Comercial</span><b>⌄</b></button>
      <div class="sidebar-group-items">
        ${navLink("/reporte.html?id=informe_general_comercial", "•", "Informe general", "informe_general_comercial")}
        ${navLink("/reporte.html?id=fuerza_comercial_diego", "•", "Fuerza Comercial", "fuerza_comercial_diego")}
        ${navLink("/reporte.html?id=rch_comercial", "•", "RCH Comercial", "rch_comercial")}
      </div>
    </div>
    <div class="sidebar-group" data-nav-group="operativa">
      <button class="sidebar-group-toggle" type="button" aria-expanded="false"><span class="sidebar-group-icon operative">⚙</span><span>Operativa</span><b>⌄</b></button>
      <div class="sidebar-group-items">${navLink("/reporte.html?id=rch_operativa", "•", "RCH Operativa", "rch_operativa")}</div>
    </div>
    <div class="sidebar-group" data-nav-group="pnnc">
      <button class="sidebar-group-toggle" type="button" aria-expanded="false"><span class="sidebar-group-icon pnnc">◇</span><span>PNNC</span><b>⌄</b></button>
      <div class="sidebar-group-items">
        ${navLink("/reporte.html?id=pnnc_comercial", "•", "PNNC Comercial", "pnnc_comercial")}
        ${navLink("/reporte.html?id=pnnc_operativa", "•", "PNNC Operativa", "pnnc_operativa")}
      </div>
    </div>
    <p>ADMINISTRACIÓN</p>
    <div class="sidebar-group" data-nav-group="administracion">
      <button class="sidebar-group-toggle" type="button" aria-expanded="false"><span class="sidebar-group-icon admin">⌘</span><span>Gestión</span><b>⌄</b></button>
      <div class="sidebar-group-items">
        ${navLink("/sincronizacion.html", "↻", "Sincronización Bitrix")}
        ${navLink("/usuarios.html", "♙", "Usuarios y roles")}
      </div>
    </div>`;

  document.querySelectorAll(".sidebar-group").forEach((group) => {
    const key = `navGroup:${group.dataset.navGroup}`;
    const hasActive = Boolean(group.querySelector("a.active"));
    const open = hasActive || localStorage.getItem(key) === "open";
    group.classList.toggle("open", open);
    group.querySelector("button").setAttribute("aria-expanded", String(open));
  });
  sessionMenu.addEventListener("click", (event) => {
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
  logoutButton.innerHTML = "<span>↪</span>Cerrar sesión";
  logoutButton.addEventListener("click", async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    sessionStorage.removeItem("adminAccessKey");
    location.href = "/login.html";
  });
  sessionMenu.append(logoutButton);
}
