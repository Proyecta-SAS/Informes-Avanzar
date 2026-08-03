let dashboard = null;
let reportCatalog = [];
let selectedReportKey = "";
let selectedPipeline = "all";

const pipelineMeta = {
  all: {
    title: "Replica Bitrix",
    description: "Snapshot local en PostgreSQL para leer negociaciones, usuarios, campos personalizados, tareas y actividades sin consultar Bitrix en cada carga.",
    label: "Pipelines iniciales",
    value: "4",
    note: "RCH y PNNC",
    tag: "Sync acotada",
    filter: "Todas"
  },
  rch_comercial: {
    title: "RCH Comercial",
    description: "Informes comerciales de la pipeline RCH: negociaciones, responsables, etapas, actividades y campos personalizados.",
    label: "Categoria Bitrix",
    value: "8",
    note: "Area comercial",
    tag: "RCH",
    filter: "RCH Comercial"
  },
  rch_operativa: {
    title: "RCH Operativa",
    description: "Informes operativos de la pipeline RCH: negociaciones, tareas, responsables, seguimiento y campos personalizados.",
    label: "Categoria Bitrix",
    value: "10",
    note: "Area operativa",
    tag: "RCH",
    filter: "RCH Operativa"
  },
  pnnc_comercial: {
    title: "PNNC Comercial",
    description: "Informes comerciales de la pipeline PNNC: oportunidades, etapas, asesores, clientes y campos personalizados.",
    label: "Categoria Bitrix",
    value: "26",
    note: "Area comercial",
    tag: "PNNC",
    filter: "PNNC Comercial"
  },
  pnnc_operativa: {
    title: "PNNC Operativa",
    description: "Informes operativos de la pipeline PNNC: tareas, actividades, responsables y estado de gestion.",
    label: "Categoria Bitrix",
    value: "28",
    note: "Area operativa",
    tag: "PNNC",
    filter: "PNNC Operativa"
  }
};

const setText = (id, value) => {
  document.getElementById(id).textContent = value;
};

const renderCards = (cards) => {
  const grid = document.getElementById("cardGrid");
  grid.innerHTML = cards
    .map((card) => `
      <article class="kpi ${card.tone}">
        <span class="card-label">${card.label}</span>
        <strong class="kpi-value">${card.value}</strong>
        <span class="kpi-note">${card.note}</span>
        <span class="kpi-detail">${card.detail}</span>
      </article>
    `)
    .join("");
};

const getVisiblePipelines = () => {
  if (!dashboard) {
    return [];
  }

  if (selectedPipeline === "all") {
    return dashboard.pipelines;
  }

  return dashboard.pipelines.filter((row) => row.slug === selectedPipeline);
};

const renderPipelineRows = () => {
  const target = document.getElementById("pipelineRows");
  const rows = getVisiblePipelines();
  target.innerHTML = rows
    .map((row) => `
      <tr>
        <td><strong>${row.name}</strong></td>
        <td>${row.categoryId}</td>
        <td>${row.area}</td>
        <td>${row.status}</td>
        <td>${row.entities}</td>
      </tr>
    `)
    .join("");
};

const renderReportTabs = () => {
  const target = document.getElementById("reportTabs");
  target.innerHTML = reportCatalog
    .map((report) => `
      <button class="${report.key === selectedReportKey ? "selected" : ""}" data-report="${report.key}" type="button">
        ${report.title}
      </button>
    `)
    .join("");

  target.querySelectorAll("button").forEach((button) => {
    button.addEventListener("click", () => {
      selectedReportKey = button.dataset.report;
      renderReportTabs();
      renderReportRows();
    });
  });
};

const renderReportRows = () => {
  const target = document.getElementById("reportRows");
  const report = reportCatalog.find((item) => item.key === selectedReportKey);
  const pipelines = getVisiblePipelines();

  if (!report) {
    target.innerHTML = "";
    return;
  }

  target.innerHTML = pipelines
    .flatMap((pipeline) =>
      report.columns.map((column) => `
        <tr>
          <td><strong>${pipeline.name}</strong></td>
          <td>${column}</td>
          <td>${report.bitrixMethod}</td>
          <td>${report.targetTable}</td>
          <td>${report.requiresScope}</td>
          <td>${report.use}</td>
        </tr>
      `)
    )
    .join("");
};

const updateSelectedContext = () => {
  const meta = pipelineMeta[selectedPipeline];
  setText("selectedTitle", meta.title);
  setText("selectedDescription", meta.description);
  setText("selectedAsideLabel", meta.label);
  setText("selectedAsideValue", meta.value);
  setText("selectedAsideNote", meta.note);
  setText("selectedAsideTag", meta.tag);
  document.getElementById("pipelineFilter").value = meta.filter;

  document.querySelectorAll("[data-pipeline]").forEach((element) => {
    element.classList.toggle("selected", element.dataset.pipeline === selectedPipeline);
    element.classList.toggle("active", element.dataset.pipeline === selectedPipeline);
  });
};

const selectPipeline = (pipeline) => {
  selectedPipeline = pipeline;
  updateSelectedContext();
  renderPipelineRows();
  renderReportRows();
};

const bindPipelineControls = () => {
  document.querySelectorAll("[data-pipeline]").forEach((element) => {
    element.addEventListener("click", (event) => {
      if (element.dataset.pipeline === "all") {
        event.preventDefault();
        selectPipeline("all");
        return;
      }

      if (element.tagName === "BUTTON") {
        window.location.href = `/informes.html?pipeline=${element.dataset.pipeline}`;
      }
    });
  });
};

const loadDashboard = async () => {
  const [dashboardResponse, reportsResponse] = await Promise.all([
    fetch("/api/dashboard/overview"),
    fetch("/api/reports/catalog")
  ]);

  dashboard = await dashboardResponse.json();
  reportCatalog = await reportsResponse.json();
  selectedReportKey = reportCatalog[0]?.key ?? "";

  renderCards(dashboard.cards);
  bindPipelineControls();
  updateSelectedContext();
  renderPipelineRows();
  renderReportTabs();
  renderReportRows();
};

loadDashboard();
