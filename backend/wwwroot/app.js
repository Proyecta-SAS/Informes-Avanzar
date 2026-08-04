const reports = [
  {
    title: "RCH Comercial - Deals",
    description: "Negociaciones comerciales RCH con etapa, responsable, valor y fecha de actualizacion.",
    pipeline: "RCH Comercial",
    entity: "Deals",
    href: "/reporte.html?id=rch_comercial"
  },
  {
    title: "RCH Operativa - Deals",
    description: "Negociaciones operativas RCH, tareas asociadas y seguimiento de gestion.",
    pipeline: "RCH Operativa",
    entity: "Deals",
    href: "/reporte.html?id=rch_operativa"
  },
  {
    title: "PNNC Comercial - Estructura",
    description: "Informe preparado para oportunidades comerciales PNNC cuando se sincronice la pipeline.",
    pipeline: "PNNC Comercial",
    entity: "Pipelines",
    href: "/reporte.html?id=pnnc_comercial"
  },
  {
    title: "PNNC Operativa - Estructura",
    description: "Informe preparado para seguimiento operativo PNNC cuando se sincronice la pipeline.",
    pipeline: "PNNC Operativa",
    entity: "Pipelines",
    href: "/reporte.html?id=pnnc_operativa"
  },
  {
    title: "Fuerza Comercial Diego",
    description: "Informe de seguimiento de la fuerza comercial de Diego.",
    pipeline: "Fuerza Comercial Diego",
    entity: "Deals",
    href: "/reporte.html?id=fuerza_comercial_diego"
  },
  {
    title: "Usuarios y responsables",
    description: "Usuarios Bitrix sincronizados para asignacion de responsables y filtros por asesor.",
    pipeline: "Global",
    entity: "Usuarios",
    href: "/usuarios.html"
  },
  {
    title: "Etapas de pipelines",
    description: "Etapas sincronizadas desde Bitrix para RCH y PNNC, con orden y estado.",
    pipeline: "Global",
    entity: "Etapas",
    href: "/informes.html"
  }
];

const renderReports = (items) => {
  document.getElementById("homeReportCount").textContent = items.length;
  document.getElementById("homeReports").innerHTML = items
    .map((report) => `
      <a class="report-card" href="${report.href}">
        <span class="status-badge">${report.entity}</span>
        <strong>${report.title}</strong>
        <p>${report.description}</p>
        <small>${report.pipeline}</small>
      </a>
    `)
    .join("");
};

const bindSearch = () => {
  const input = document.getElementById("reportSearch");
  input.addEventListener("input", () => {
    const query = input.value.trim().toLowerCase();
    const filtered = reports.filter((report) =>
      `${report.title} ${report.description} ${report.pipeline} ${report.entity}`
        .toLowerCase()
        .includes(query)
    );
    renderReports(filtered);
  });
};

renderReports(reports);
bindSearch();
