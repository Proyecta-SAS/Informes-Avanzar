const pipelines = {
  all: {
    name: "Todas las pipelines",
    category: "4",
    area: "Todas las areas",
    tag: "Global",
    description: "Vista consolidada para comparar RCH Comercial, RCH Operativa, PNNC Comercial y PNNC Operativa."
  },
  rch_comercial: {
    name: "RCH Comercial",
    category: "8",
    area: "Area comercial",
    tag: "RCH",
    description: "Informes comerciales de RCH: negociaciones, etapas, usuarios responsables y campos personalizados."
  },
  rch_operativa: {
    name: "RCH Operativa",
    category: "10",
    area: "Area operativa",
    tag: "RCH",
    description: "Informes operativos de RCH: tareas, actividades, responsables y seguimiento de gestion."
  },
  pnnc_comercial: {
    name: "PNNC Comercial",
    category: "26",
    area: "Area comercial",
    tag: "PNNC",
    description: "Informes comerciales de PNNC: oportunidades, asesores, etapas y clientes."
  },
  pnnc_operativa: {
    name: "PNNC Operativa",
    category: "28",
    area: "Area operativa",
    tag: "PNNC",
    description: "Informes operativos de PNNC: tareas, actividades, responsables y vencimientos."
  }
};

let catalog = [];
let selectedPipeline = new URLSearchParams(window.location.search).get("pipeline") ?? "all";
let selectedReport = "usuarios";

const setText = (id, value) => {
  document.getElementById(id).textContent = value;
};

const getPipelineRows = () => {
  if (selectedPipeline === "all") {
    return Object.entries(pipelines)
      .filter(([key]) => key !== "all")
      .map(([key, value]) => ({ key, ...value }));
  }

  return [{ key: selectedPipeline, ...pipelines[selectedPipeline] }];
};

const renderContext = () => {
  const pipeline = pipelines[selectedPipeline] ?? pipelines.all;
  setText("pageTitle", pipeline.name);
  setText("pipelineTitle", pipeline.name);
  setText("pipelineDescription", pipeline.description);
  setText("pipelineCategory", pipeline.category);
  setText("pipelineArea", pipeline.area);
  setText("pipelineTag", pipeline.tag);
  document.getElementById("selectedPipelineInput").value = pipeline.name;

  document.querySelectorAll("[data-pipeline-link]").forEach((link) => {
    link.classList.toggle("active", link.dataset.pipelineLink === selectedPipeline);
  });
};

const renderReport = () => {
  const report = catalog.find((item) => item.key === selectedReport);
  if (!report) return;

  setText("reportTitle", report.title);
  setText("reportUse", report.use);
  document.getElementById("selectedReportInput").value = report.title;

  document.querySelectorAll("[data-report]").forEach((button) => {
    button.classList.toggle("selected", button.dataset.report === selectedReport);
  });

  document.getElementById("reportRows").innerHTML = getPipelineRows()
    .flatMap((pipeline) => report.columns.map((column) => `
      <tr>
        <td><strong>${pipeline.name}</strong></td>
        <td>${column}</td>
        <td>${report.bitrixMethod}</td>
        <td>${report.targetTable}</td>
        <td>${report.requiresScope}</td>
        <td><span class="status-badge">Pendiente sync</span></td>
      </tr>
    `))
    .join("");
};

const bindTabs = () => {
  document.querySelectorAll("[data-report]").forEach((button) => {
    button.addEventListener("click", () => {
      selectedReport = button.dataset.report;
      renderReport();
    });
  });
};

const load = async () => {
  const response = await fetch("/api/reports/catalog");
  catalog = await response.json();

  if (!pipelines[selectedPipeline]) {
    selectedPipeline = "all";
  }

  renderContext();
  bindTabs();
  renderReport();
};

load();
