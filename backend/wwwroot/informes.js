const dashboards = [
  {
    id: "rch_comercial",
    icon: "💼",
    title: "RCH Comercial",
    area: "Comercial",
    description: "Seguimiento comercial de negociaciones RCH, responsables, etapas y actividad reciente.",
    status: "Sincronizado",
    metrics: [
      { label: "Deals", value: "546" },
      { label: "Etapas", value: "31" },
      { label: "Usuarios", value: "477" }
    ]
  },
  {
    id: "rch_operativa",
    icon: "🧩",
    title: "RCH Operativa",
    area: "Operaciones",
    description: "Vista operativa para control de pipeline, carga de gestion y seguimiento de casos RCH.",
    status: "Sincronizado",
    metrics: [
      { label: "Deals", value: "607" },
      { label: "Etapas", value: "31" },
      { label: "Usuarios", value: "477" }
    ]
  },
  {
    id: "pnnc_comercial",
    icon: "📈",
    title: "PNNC Comercial",
    area: "Comercial",
    description: "Dashboard comercial PNNC preparado para publicar cuando se complete la sincronizacion.",
    status: "Pendiente sync",
    metrics: [
      { label: "Deals", value: "0" },
      { label: "Etapas", value: "40" },
      { label: "Usuarios", value: "477" }
    ]
  },
  {
    id: "pnnc_operativa",
    icon: "⚙️",
    title: "PNNC Operativa",
    area: "Operaciones",
    description: "Dashboard operativo PNNC para tareas, etapas y estado de gestion.",
    status: "Pendiente sync",
    metrics: [
      { label: "Deals", value: "0" },
      { label: "Etapas", value: "40" },
      { label: "Usuarios", value: "477" }
    ]
  }
];

const renderDashboards = (items) => {
  document.getElementById("visibleReports").textContent = items.length;
  document.getElementById("dashboardGrid").innerHTML = items
    .map((dashboard) => `
      <a class="dashboard-card" href="/reporte.html?id=${dashboard.id}">
        <div class="dashboard-preview">
          <div class="preview-topline"></div>
          <div class="preview-bars">
            <span style="height: 42%"></span>
            <span style="height: 70%"></span>
            <span style="height: 54%"></span>
            <span style="height: 88%"></span>
          </div>
          <div class="preview-table">
            <span></span><span></span><span></span>
          </div>
        </div>
        <div class="dashboard-body">
          <span class="dashboard-icon">${dashboard.icon}</span>
          <div>
            <strong>${dashboard.title}</strong>
            <p>${dashboard.description}</p>
          </div>
        </div>
        <div class="dashboard-metrics">
          ${dashboard.metrics.map((metric) => `
            <span><b>${metric.value}</b>${metric.label}</span>
          `).join("")}
        </div>
        <div class="dashboard-footer">
          <small>${dashboard.area}</small>
          <em>${dashboard.status}</em>
        </div>
      </a>
    `)
    .join("");
};

const bindSearch = () => {
  document.getElementById("dashboardSearch").addEventListener("input", (event) => {
    const query = event.target.value.trim().toLowerCase();
    const filtered = dashboards.filter((dashboard) =>
      `${dashboard.title} ${dashboard.area} ${dashboard.description} ${dashboard.status}`
        .toLowerCase()
        .includes(query)
    );
    renderDashboards(filtered);
  });
};

renderDashboards(dashboards);
bindSearch();
