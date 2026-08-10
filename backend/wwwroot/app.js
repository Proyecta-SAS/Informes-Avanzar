const areas = [
  {
    id: "comercial",
    icon: "↗",
    title: "Comercial",
    description: "Seguimiento de oportunidades, radicación y desempeño de la fuerza comercial.",
    tone: "violet",
    reports: [
      { title: "Informe General Comercial", description: "Radicación, negociaciones, comisiones, cartera, embudos y etapas en una sola vista.", href: "/reporte.html?id=informe_general_comercial", badge: "Informe general" },
      { title: "Marketing", description: "Espacio para campañas, generación de demanda y métricas de marketing.", badge: "Próximamente", upcoming: true }
    ]
  },
  {
    id: "operativo",
    icon: "⚙",
    title: "Operativo",
    description: "Control operativo, documentación y avance de los casos radicados.",
    tone: "blue",
    reports: [
      { title: "PNNC Operativa", description: "Casos, documentación y etapas operativas PNNC.", href: "/reporte.html?id=pnnc_operativa", badge: "Operativa" },
      { title: "RCH Operativa", description: "Gestión operativa de negociaciones y etapas RCH.", href: "/reporte.html?id=rch_operativa", badge: "Pipeline" },
      { title: "LP Operativa", description: "Espacio para el seguimiento operativo de la línea LP.", badge: "Próximamente", upcoming: true }
    ]
  },
  {
    id: "administrativo",
    icon: "▦",
    title: "Administrativo",
    description: "Indicadores administrativos, financieros y de soporte corporativo.",
    tone: "red",
    reports: []
  },
  {
    id: "gerencia",
    icon: "◇",
    title: "Gerencia",
    description: "Tableros ejecutivos y consolidación estratégica para la gerencia.",
    tone: "violet",
    reports: [
      { title: "Informe Gerencia 2026 y 2027", description: "Panel ejecutivo para seguimiento gerencial de indicadores 2026 y 2027.", href: "/reporte.html?id=informe_gerencia_2026_2027", badge: "Gerencia" }
    ]
  },
  {
    id: "subgerencia",
    icon: "◫",
    title: "Subgerencia",
    description: "Seguimiento de gestión, objetivos y resultados de subgerencia.",
    tone: "blue",
    reports: []
  }
];

let homeSession = { roleCode: "", accessibleReportCodes: [], permissions: [] };
const reportCodeFromHref = (href) => new URL(href, location.origin).searchParams.get("id");
const isCommercialScoped = () => ["coordinator", "leader"].includes(homeSession.commercialRole);

const renderAreas = (query = "") => {
  const normalized = query.trim().toLocaleLowerCase("es-CO");
  let visibleReports = 0;
  const visibleAreas = isCommercialScoped()
    ? areas.filter((area) => area.id === "comercial")
    : areas;
  const content = visibleAreas.map((area) => {
    const areaMatch = `${area.title} ${area.description}`.toLocaleLowerCase("es-CO").includes(normalized);
    const allowed = new Set(homeSession.accessibleReportCodes ?? []);
    const canSeePlaceholders = homeSession.roleCode === "admin";
    const reports = area.reports.filter((report) => canSeePlaceholders || allowed.has(reportCodeFromHref(report.href)))
      .filter((report) => areaMatch || `${report.title} ${report.description} ${report.badge}`.toLocaleLowerCase("es-CO").includes(normalized));
    if (!reports.length && !canSeePlaceholders) return "";
    if (!reports.length && normalized && !areaMatch) return "";
    visibleReports += reports.filter((report) => !report.upcoming).length;
    return `<article id="${area.id}" class="home-area-card ${area.tone}">
      <header data-area-toggle><span>${area.icon}</span><div><small>Área</small><h3>${area.title}</h3></div><em>${reports.length ? `${reports.length} ${reports.length === 1 ? "módulo" : "módulos"}` : "Disponible"}</em><button type="button" aria-expanded="false" aria-label="Abrir ${area.title}">⌄</button></header>
      <div class="area-card-content" hidden><p>${area.description}</p>
      <div class="home-report-links">${reports.length ? reports.map((report) => report.upcoming
        ? `<div class="home-report-placeholder"><div><span>${report.badge}</span><strong>${report.title}</strong><small>${report.description}</small></div><b>En preparación</b></div>`
        : `<a href="${report.href}"><div><span>${report.badge}</span><strong>${report.title}</strong><small>${report.description}</small></div><b>→</b></a>`).join("")
        : `<div class="home-area-empty"><strong>Área preparada</strong><small>Los informes se agregarán cuando sean definidos.</small></div>`}</div></div>
    </article>`;
  }).join("");
  document.getElementById("homeAreas").innerHTML = content || `<div class="home-no-results"><strong>No encontramos informes</strong><span>Prueba con otro nombre o área.</span></div>`;
  document.getElementById("homeResultCount").textContent = `${visibleReports} ${visibleReports === 1 ? "acceso" : "accesos"}`;
};

document.getElementById("reportSearch").addEventListener("input", (event) => renderAreas(event.target.value));
document.getElementById("homeAreas").addEventListener("click", (event) => {
  const header = event.target.closest("[data-area-toggle]");
  if (!header) return;
  const card = header.closest(".home-area-card");
  const content = card.querySelector(".area-card-content");
  const open = !card.classList.contains("open");
  card.classList.toggle("open", open);
  content.hidden = !open;
  header.querySelector("button").setAttribute("aria-expanded", String(open));
});
fetch("/api/auth/me").then((response) => response.json()).then((session) => {
  homeSession = session;
  const permissions = new Set(session.permissions ?? []);
  document.querySelectorAll("[data-required-permission]").forEach((item) => {
    const required = item.dataset.requiredPermission.split(" ");
    if (isCommercialScoped() || (session.roleCode !== "admin" && !required.some((code) => permissions.has(code)))) item.remove();
  });
  document.querySelectorAll("[data-admin-only]").forEach((item) => item.hidden = session.roleCode !== "admin");
  document.querySelector(".home-avatar").textContent = session.fullName?.charAt(0).toUpperCase() ?? "U";
  renderAreas();
}).catch(() => renderAreas());
