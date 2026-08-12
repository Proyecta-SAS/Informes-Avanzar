const reportAreas = [
  {
    id: "comercial",
    icon: "↗",
    title: "Comercial",
    tone: "violet",
    description: "Seguimiento de oportunidades, radicación y desempeño de la fuerza comercial.",
    reports: [
      { id: "informe_general_comercial", title: "Informe General Comercial", badge: "Informe general", description: "Radicación, negociaciones, comisiones, cartera, embudos y etapas en una sola vista." }
    ]
  },
  {
    id: "operativo",
    icon: "⚙",
    title: "Operativo",
    tone: "blue",
    description: "Control operativo, documentación y avance de los casos radicados.",
    reports: [
      { id: "pnnc_operativa", title: "PNNC Operativa", badge: "Operativa", description: "Casos, documentación y etapas operativas PNNC." },
      { id: "rch_operativa", title: "RCH Operativa", badge: "Pipeline", description: "Gestión operativa de negociaciones y etapas RCH." },
      { id: "lp_operativa", title: "LP Operativa", badge: "Próximamente", description: "Espacio para el seguimiento operativo de la línea LP.", upcoming: true }
    ]
  },
  {
    id: "administrativo",
    icon: "▦",
    title: "Administrativo",
    tone: "red",
    description: "Indicadores administrativos, financieros y de soporte corporativo.",
    reports: []
  },
  {
    id: "gerencia",
    icon: "◇",
    title: "Gerencia",
    tone: "violet",
    description: "Tableros ejecutivos y consolidación estratégica para la gerencia.",
    reports: [
      { id: "informe_gerencia_2026_2027", title: "Informe Gerencia 2026 y 2027", badge: "Gerencia", description: "Panel ejecutivo para seguimiento gerencial de indicadores 2026 y 2027." }
    ]
  },
  {
    id: "subgerencia",
    icon: "◫",
    title: "Subgerencia",
    tone: "blue",
    description: "Seguimiento de gestión, objetivos y resultados de subgerencia.",
    reports: []
  }
];

let reportsSession = { roleCode: "", accessibleReportCodes: [], permissions: [] };

const renderReports = (query = "") => {
  const normalized = query.trim().toLocaleLowerCase("es-CO");
  let visibleReports = 0;
  const content = reportAreas.map((area) => {
    const areaMatches = area.title.toLocaleLowerCase("es-CO").includes(normalized);
    const allowed = new Set(reportsSession.accessibleReportCodes ?? []);
    const canSeePlaceholders = reportsSession.roleCode === "admin";
    const reports = area.reports.filter((report) => canSeePlaceholders || allowed.has(report.id)).filter((report) => areaMatches
      || `${report.title} ${report.badge} ${report.description}`.toLocaleLowerCase("es-CO").includes(normalized));
    if (!reports.length && !canSeePlaceholders) return "";
    if (!reports.length && normalized && !areaMatches) return "";
    visibleReports += reports.filter((report) => !report.upcoming).length;
    return `<article id="${area.id}" class="catalog-area-card ${area.tone}">
      <header data-area-toggle><span>${area.icon}</span><div><small>Área</small><h3>${area.title}</h3></div><em>${reports.length ? `${reports.length} ${reports.length === 1 ? "módulo" : "módulos"}` : "Disponible"}</em><button type="button" aria-expanded="false" aria-label="Abrir ${area.title}">⌄</button></header>
      <div class="area-card-content" hidden><p>${area.description}</p>
      <div class="catalog-report-links">${reports.length ? reports.map((report) => report.upcoming
        ? `<div class="catalog-report-placeholder"><div><span>${report.badge}</span><strong>${report.title}</strong><small>${report.description}</small></div><b>En preparación</b></div>`
        : `<a href="/reporte.html?id=${report.id}"><div><span>${report.badge}</span><strong>${report.title}</strong><small>${report.description}</small></div><b>→</b></a>`).join("")
        : `<div class="catalog-area-empty"><strong>Área preparada</strong><small>Los informes se agregarán cuando sean definidos.</small></div>`}</div></div>
    </article>`;
  }).join("");

  document.getElementById("visibleReports").textContent = visibleReports;
  document.getElementById("dashboardGrid").innerHTML = content
    || `<div class="catalog-empty"><strong>No encontramos informes</strong><span>Prueba con otro nombre o área.</span></div>`;
};

document.getElementById("dashboardSearch").addEventListener("input", (event) => renderReports(event.target.value));
document.getElementById("dashboardGrid").addEventListener("click", (event) => {
  const header = event.target.closest("[data-area-toggle]");
  if (!header) return;
  const card = header.closest(".catalog-area-card");
  const content = card.querySelector(".area-card-content");
  const open = !card.classList.contains("open");
  card.classList.toggle("open", open);
  content.hidden = !open;
  header.querySelector("button").setAttribute("aria-expanded", String(open));
});
fetch("/api/auth/me").then((response) => response.json()).then((session) => {
  reportsSession = session;
  document.querySelector(".profile strong").textContent = session.fullName ?? "Usuario";
  document.querySelector(".profile small").textContent = session.roleCode === "admin" ? "Administrador" : "Usuario del panel";
  document.querySelector(".profile .avatar").textContent = session.fullName?.charAt(0).toUpperCase() ?? "U";
  renderReports();
}).catch(() => renderReports());
