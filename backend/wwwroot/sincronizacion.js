const formatNumber = new Intl.NumberFormat("es-CO");
const pipelines = [
  { slug: "rch_comercial", name: "RCH Comercial", categoryId: 8 },
  { slug: "rch_operativa", name: "RCH Operativa", categoryId: 10 },
  { slug: "pnnc_comercial", name: "PNNC Comercial", categoryId: 26 },
  { slug: "pnnc_operativa", name: "PNNC Operativa", categoryId: 28 }
];

let stagesByPipeline = new Map();

const setSyncButtonsDisabled = (disabled) => {
  document.querySelectorAll("#incrementalButton, #massiveButton, #stageButton, [data-pipeline-sync], [data-pipeline-incremental]").forEach((button) => {
    const hasNoStage = button.id === "stageButton" && !document.getElementById("stageSelect")?.value;
    button.disabled = disabled || hasNoStage;
  });
};

const setStatus = (status, message) => {
  document.getElementById("syncStatus").textContent = status;
  document.getElementById("syncMessage").textContent = message;
};

const formatDate = (value) => {
  if (!value) return "";
  return new Intl.DateTimeFormat("es-CO", { dateStyle: "short", timeStyle: "short" }).format(new Date(value));
};

const loadState = async () => {
  const response = await fetch("/api/data/sync-state");
  const state = await response.json();
  setSyncButtonsDisabled(state.isSyncing);

  if (!state.isSyncing) {
    setStatus("Listo", "Sin sincronizaciones activas");
    return false;
  }

  const active = state.activeRun;
  setStatus(
    "Sincronizando",
    `${active.entityType}: ${formatNumber.format(active.recordsRead ?? 0)} leidos / ${formatNumber.format(active.recordsWritten ?? 0)} nuevos o actualizados`
  );
  return true;
};

const loadHistory = async () => {
  const response = await fetch("/api/data/sync-history");
  const rows = await response.json();
  document.getElementById("syncHistoryRows").innerHTML = rows.map((row) => `
    <tr>
      <td>${row.entityType}</td>
      <td>${row.status}</td>
      <td>${formatNumber.format(row.recordsRead ?? 0)}</td>
      <td>${formatNumber.format(row.recordsWritten ?? 0)}</td>
      <td>${formatDate(row.createdAt)}</td>
      <td>${formatDate(row.finishedAt)}</td>
    </tr>
  `).join("");
};

const loadStages = async () => {
  const entries = await Promise.all(pipelines.map(async (pipeline) => {
    const response = await fetch(`/api/data/stages?pipeline=${encodeURIComponent(pipeline.slug)}`);
    const stages = await response.json();
    return [pipeline.slug, stages];
  }));

  stagesByPipeline = new Map(entries);
};

const renderStageSelector = () => {
  const pipelineSelect = document.getElementById("pipelineSelect");
  const stageSelect = document.getElementById("stageSelect");
  const selectedPipeline = pipelineSelect.value || pipelines[0].slug;
  const stages = stagesByPipeline.get(selectedPipeline) ?? [];

  pipelineSelect.innerHTML = pipelines.map((pipeline) => `
    <option value="${pipeline.slug}"${pipeline.slug === selectedPipeline ? " selected" : ""}>${pipeline.name}</option>
  `).join("");

  stageSelect.innerHTML = stages.length
    ? stages.map((stage) => `<option value="${stage.stageId}">${stage.name} (${stage.stageId})</option>`).join("")
    : `<option value="">Sin etapas guardadas</option>`;

  document.getElementById("stageButton").disabled = stages.length === 0;
};

const renderPipelineRows = () => {
  document.getElementById("pipelineRows").innerHTML = pipelines.map((pipeline) => {
    const stages = stagesByPipeline.get(pipeline.slug) ?? [];
    return `
      <tr>
        <td><strong>${pipeline.name}</strong></td>
        <td>${pipeline.categoryId}</td>
        <td>${formatNumber.format(stages.length)}</td>
        <td>
          <button data-pipeline-incremental="${pipeline.slug}" type="button">Solo cambios</button>
          <button data-pipeline-sync="${pipeline.slug}" type="button">Completa</button>
        </td>
      </tr>
    `;
  }).join("");

  document.querySelectorAll("[data-pipeline-sync]").forEach((button) => {
    button.addEventListener("click", async () => {
      await runSync(`/api/bitrix/sync/deals/${encodeURIComponent(button.dataset.pipelineSync)}`);
    });
  });

  document.querySelectorAll("[data-pipeline-incremental]").forEach((button) => {
    button.addEventListener("click", async () => {
      await runSync(`/api/bitrix/sync/deals/${encodeURIComponent(button.dataset.pipelineIncremental)}/incremental`);
    });
  });
};

const reload = async () => {
  await loadStages();
  renderStageSelector();
  renderPipelineRows();
  await loadState();
  await loadHistory();
};

const runSync = async (url) => {
  setSyncButtonsDisabled(true);
  setStatus("Iniciando", "Arrancando sincronizacion en segundo plano");
  await fetch(url, { method: "POST" });
  await reload();
};

document.getElementById("massiveButton").addEventListener("click", async () => {
  await runSync("/api/bitrix/sync/massive");
});

document.getElementById("incrementalButton").addEventListener("click", async () => {
  await runSync("/api/bitrix/sync/global/incremental");
});

document.getElementById("stageButton").addEventListener("click", async () => {
  const pipeline = document.getElementById("pipelineSelect").value;
  const stageId = document.getElementById("stageSelect").value;
  await runSync(`/api/bitrix/sync/deals/${encodeURIComponent(pipeline)}?stageId=${encodeURIComponent(stageId)}`);
});

document.getElementById("pipelineSelect").addEventListener("change", renderStageSelector);
document.getElementById("refreshButton").addEventListener("click", reload);

reload();
setInterval(reload, 5000);
