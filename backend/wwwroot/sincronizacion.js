const formatNumber = new Intl.NumberFormat("es-CO");

let pipelines = [];
let stagesByPipeline = new Map();
let syncInProgress = false;

const escapeHtml = (value) => String(value ?? "")
  .replaceAll("&", "&amp;")
  .replaceAll("<", "&lt;")
  .replaceAll(">", "&gt;")
  .replaceAll('"', "&quot;")
  .replaceAll("'", "&#039;");

const setSyncButtonsDisabled = (disabled) => {
  document.querySelectorAll("#commercialQuickButton, #incrementalButton, #massiveButton, #stageButton, [data-pipeline-sync], [data-pipeline-incremental]").forEach((button) => {
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
  if (!response.ok) throw new Error(`No fue posible consultar el estado: HTTP ${response.status}`);
  const state = await response.json();
  syncInProgress = state.isSyncing;
  setSyncButtonsDisabled(syncInProgress);

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
  if (!response.ok) throw new Error(`No fue posible consultar el historial: HTTP ${response.status}`);
  const rows = await response.json();
  document.getElementById("syncHistoryRows").innerHTML = rows.map((row) => `
    <tr>
      <td>${escapeHtml(row.entityType)}</td>
      <td>${escapeHtml(row.status)}</td>
      <td>${formatNumber.format(row.recordsRead ?? 0)}</td>
      <td>${formatNumber.format(row.recordsWritten ?? 0)}</td>
      <td>${formatDate(row.createdAt)}</td>
      <td>${formatDate(row.finishedAt)}</td>
    </tr>
  `).join("");
};

const loadInventory = async () => {
  const response = await fetch("/api/data/pipeline-inventory");
  if (!response.ok) throw new Error(`No fue posible consultar el inventario: HTTP ${response.status}`);
  const inventory = await response.json();

  pipelines = inventory.pipelines ?? [];
  stagesByPipeline = new Map(pipelines.map((pipeline) => [pipeline.slug, pipeline.stages ?? []]));
  document.getElementById("databaseDealsTotal").textContent = formatNumber.format(inventory.totalDeals ?? 0);
};

const renderStageSelector = () => {
  const pipelineSelect = document.getElementById("pipelineSelect");
  const stageSelect = document.getElementById("stageSelect");
  const previousPipeline = pipelineSelect.value;
  const selectedPipeline = pipelines.some((pipeline) => pipeline.slug === previousPipeline)
    ? previousPipeline
    : pipelines[0]?.slug;

  pipelineSelect.innerHTML = pipelines.length
    ? pipelines.map((pipeline) => `
        <option value="${escapeHtml(pipeline.slug)}"${pipeline.slug === selectedPipeline ? " selected" : ""}>${escapeHtml(pipeline.name)}</option>
      `).join("")
    : `<option value="">Sin pipelines activas</option>`;

  const stages = (stagesByPipeline.get(selectedPipeline) ?? []).filter((stage) => stage.stageId);
  stageSelect.innerHTML = stages.length
    ? stages.map((stage) => `<option value="${escapeHtml(stage.stageId)}">${escapeHtml(stage.stageName)} (${escapeHtml(stage.stageId)})</option>`).join("")
    : `<option value="">Sin etapas guardadas</option>`;

  document.getElementById("stageButton").disabled = stages.length === 0;
};

const renderStageInventory = (pipeline) => {
  const stages = pipeline.stages ?? [];
  if (!stages.length) {
    return `<p class="panel-subtitle">Esta pipeline no tiene etapas ni negociaciones guardadas.</p>`;
  }

  return `
    <div class="stage-inventory-wrap">
      <table class="stage-inventory-table">
        <thead>
          <tr><th>Etapa</th><th>ID Bitrix</th><th>Negociaciones en BD</th></tr>
        </thead>
        <tbody>
          ${stages.map((stage) => `
            <tr>
              <td class="${stage.isUnmapped ? "stage-unmapped" : ""}">${escapeHtml(stage.stageName)}</td>
              <td><code>${escapeHtml(stage.stageId ?? "-")}</code></td>
              <td><strong>${formatNumber.format(stage.dealsCount ?? 0)}</strong></td>
            </tr>
          `).join("")}
        </tbody>
      </table>
    </div>`;
};

const renderPipelineRows = () => {
  document.getElementById("pipelineRows").innerHTML = pipelines.length
    ? pipelines.map((pipeline) => {
        const stages = pipeline.stages ?? [];
        return `
          <tr>
            <td><strong>${escapeHtml(pipeline.name)}</strong><br><small>${escapeHtml(pipeline.slug)}</small></td>
            <td><code>${pipeline.categoryId}</code></td>
            <td><strong>${formatNumber.format(pipeline.dealsCount ?? 0)}</strong></td>
            <td>${formatNumber.format(stages.length)}</td>
            <td>
              <div class="pipeline-row-actions">
                <button data-pipeline-incremental="${escapeHtml(pipeline.slug)}" type="button">Solo cambios</button>
                <button data-pipeline-sync="${escapeHtml(pipeline.slug)}" type="button">Completa</button>
              </div>
            </td>
          </tr>
          <tr class="pipeline-stage-detail">
            <td colspan="5">
              <details>
                <summary>Ver negociaciones por etapa (${formatNumber.format(stages.length)} etapas)</summary>
                ${renderStageInventory(pipeline)}
              </details>
            </td>
          </tr>`;
      }).join("")
    : `<tr><td colspan="5">No hay pipelines activas en PostgreSQL.</td></tr>`;

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

  setSyncButtonsDisabled(syncInProgress);
};

const reloadInventory = async () => {
  await loadInventory();
  renderStageSelector();
  renderPipelineRows();
};

const reloadActivity = async () => {
  await Promise.all([loadState(), loadHistory()]);
};

const reload = async () => {
  try {
    await Promise.all([reloadInventory(), reloadActivity()]);
  } catch (error) {
    setStatus("Error", error.message);
  }
};

const runSync = async (url) => {
  if (syncInProgress) {
    setStatus("Sincronizando", "Espera a que termine la sincronizacion activa.");
    return;
  }

  try {
    if (await loadState()) return;
  } catch (error) {
    setStatus("Error", error.message);
    setSyncButtonsDisabled(false);
    return;
  }

  syncInProgress = true;
  setSyncButtonsDisabled(true);
  setStatus("Iniciando", "Arrancando sincronizacion");
  const response = await fetch(url, { method: "POST" });
  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    if (response.status === 409) {
      await reloadActivity().catch(() => {});
      setStatus("Sincronizando", error.message ?? "Ya hay otra sincronizacion activa.");
      return;
    }
    setStatus("Error", error.message ?? `La sincronizacion no pudo iniciar: HTTP ${response.status}`);
    await loadState().catch(() => setSyncButtonsDisabled(false));
    return;
  }
  await reload();
};

document.getElementById("massiveButton").addEventListener("click", async () => {
  await runSync("/api/bitrix/sync/massive");
});

document.getElementById("commercialQuickButton").addEventListener("click", async () => {
  await runSync("/api/bitrix/sync/reports/comercial/quick");
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
setInterval(() => reloadActivity().catch((error) => setStatus("Error", error.message)), 5000);
setInterval(() => reloadInventory().catch((error) => setStatus("Error", error.message)), 60000);
