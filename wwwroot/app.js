// ==========================================
// DEBUG: Check if everything is loaded
// ==========================================
console.log('📦 app.js loaded');
console.log('✓ THREE available:', typeof window.THREE !== 'undefined');
console.log('✓ THREE.OrbitControls available:', typeof window.THREE?.OrbitControls !== 'undefined');
console.log('✓ Viewer3D class available:', typeof window.Viewer3D !== 'undefined');

// Helper function to wait for Viewer3D class to be available
async function ensureViewer3D(maxWait = 5000) {
    const startTime = Date.now();
    while (typeof window.Viewer3D === 'undefined' && Date.now() - startTime < maxWait) {
        console.log('⏳ Waiting for Viewer3D class...');
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    
    if (typeof window.Viewer3D === 'undefined') {
        throw new Error('Viewer3D class did not load within timeout');
    }
    
    console.log('✓ Viewer3D class is now available');
    return window.Viewer3D;
}

// Wait for DOMContentLoaded to ensure all scripts loaded
if (typeof window.three3DLoaded === 'undefined') {
    window.three3DLoaded = false;
    window.addEventListener('DOMContentLoaded', () => {
        console.log('=== DOMContentLoaded fired ===');
        console.log('✓ THREE:', typeof window.THREE);
        console.log('✓ THREE.Scene:', typeof window.THREE?.Scene);
        console.log('✓ THREE.OrbitControls:', typeof window.THREE?.OrbitControls);
        console.log('✓ Viewer3D:', typeof window.Viewer3D);
        window.three3DLoaded = true;
    });
}
// ==========================================

const uploadArea = document.getElementById('uploadArea');
const fileInput = document.getElementById('fileInput');
const previewContainer = document.getElementById('previewContainer');
const generateBtn = document.getElementById('generateBtn');
const loadingSpinner = document.getElementById('loadingSpinner');
const resultCard = document.getElementById('resultCard');
const errorCard = document.getElementById('errorCard');
const downloadBtn = document.getElementById('downloadBtn');

let selectedFile = null;
let generatedData = null;
let viewer3D = null;
let worldSessionId = null;
let previewDataUrl = null;

const worldInitBtn = document.getElementById('worldInitBtn');
const worldStepForwardBtn = document.getElementById('worldStepForwardBtn');
const worldTurnLeftBtn = document.getElementById('worldTurnLeftBtn');
const worldTurnRightBtn = document.getElementById('worldTurnRightBtn');
const worldStyleSelect = document.getElementById('worldStyleSelect');
const worldApplyStyleBtn = document.getElementById('worldApplyStyleBtn');
const worldAddObjectBtn = document.getElementById('worldAddObjectBtn');
const worldSessionInput = document.getElementById('worldSessionInput');
const worldSaveBtn = document.getElementById('worldSaveBtn');
const worldLoadBtn = document.getElementById('worldLoadBtn');

// File upload handlers
uploadArea.addEventListener('click', () => fileInput.click());

uploadArea.addEventListener('dragover', (e) => {
    e.preventDefault();
    e.stopPropagation();
    uploadArea.classList.add('dragover');
});

uploadArea.addEventListener('dragleave', (e) => {
    e.preventDefault();
    e.stopPropagation();
    uploadArea.classList.remove('dragover');
});

uploadArea.addEventListener('drop', (e) => {
    e.preventDefault();
    e.stopPropagation();
    uploadArea.classList.remove('dragover');
    
    const files = e.dataTransfer.files;
    if (files.length > 0) {
        handleFileSelect(files[0]);
    }
});

fileInput.addEventListener('change', (e) => {
    if (e.target.files.length > 0) {
        handleFileSelect(e.target.files[0]);
    }
});

function handleFileSelect(file) {
    if (!file.type.startsWith('image/')) {
        showError('Пожалуйста, выберите изображение (PNG, JPG, JPEG)');
        return;
    }

    if (file.size > 50 * 1024 * 1024) { // 50MB limit
        showError('Файл слишком большой (максимум 50MB)');
        return;
    }

    selectedFile = file;
    generateBtn.disabled = false;
    document.getElementById('export3dObjBtn').disabled = false;
    document.getElementById('export3dGltfBtn').disabled = false;
    document.getElementById('export3dPackageBtn').disabled = false;
    errorCard.style.display = 'none';

    // Preview image
    const reader = new FileReader();
    reader.onload = (e) => {
        previewDataUrl = e.target.result;
        previewContainer.innerHTML = `<img src="${previewDataUrl}" alt="Preview" style="max-width: 100%; max-height: 100%; object-fit: contain;">`;
        if (viewer3D && typeof viewer3D.setSourceImage === 'function') {
            viewer3D.setSourceImage(previewDataUrl);
        }
    };
    reader.onerror = () => {
        showError('Ошибка при чтении файла');
    };
    reader.readAsDataURL(file);
}

// 3D Export - OBJ
document.getElementById('export3dObjBtn').addEventListener('click', async () => {
    await export3D('obj', '/api/export-3d/obj', 'OBJ');
});

// 3D Export - GLTF
document.getElementById('export3dGltfBtn').addEventListener('click', async () => {
    await export3D('gltf', '/api/export-3d/gltf', 'GLTF');
});

// 3D Export - Package
document.getElementById('export3dPackageBtn').addEventListener('click', async () => {
    await export3DPackage();
});

async function export3D(format, endpoint, formatName) {
    if (!selectedFile) {
        showError('Пожалуйста, выберите изображение');
        return;
    }

    loadingSpinner.style.display = 'block';
    
    const formData = new FormData();
    formData.append('file', selectedFile);

    try {
        console.log(`🎨 Экспортирую в ${formatName}...`);
        const response = await fetch(endpoint, {
            method: 'POST',
            body: formData,
            headers: {
                'Accept': 'application/json'
            }
        });

        const data = await response.json();

        if (response.ok && data.success) {
            console.log(`✓ ${formatName} экспорт успешен`, data);
            
            let statsMsg = `✓ ${formatName} экспорт выполнен!\n\n`;
            statsMsg += `� Сеток: ${data.statistics.meshes}\n`;
            statsMsg += `⬜️ Вершин: ${data.statistics.vertices.toLocaleString()}\n`;
            statsMsg += `▲ Треугольников: ${data.statistics.triangles.toLocaleString()}\n`;
            statsMsg += `📦 Платформ: ${data.statistics.platforms}\n`;
            statsMsg += `⚠️ Препятствий: ${data.statistics.obstacles}`;
            
            alert(statsMsg);

            if (format === 'obj' && data.obj_data) {
                downloadFile(data.obj_data, `level.obj`, 'text/plain');
            } else if (format === 'gltf' && data.gltf_data) {
                downloadFile(data.gltf_data, `level.gltf`, 'application/json');
                if (data.materials) {
                    downloadFile(data.materials, `level.mtl`, 'text/plain');
                }
            }
        } else {
            showError(data.error || `Ошибка при экспорте в ${formatName}`);
        }
    } catch (error) {
        console.error(`✗ Ошибка ${formatName}:`, error);
        showError(`Ошибка экспорта ${formatName}: ` + error.message);
    } finally {
        loadingSpinner.style.display = 'none';
    }
}

async function export3DPackage() {
    if (!selectedFile) {
        showError('Пожалуйста, выберите изображение');
        return;
    }

    loadingSpinner.style.display = 'block';
    
    const formData = new FormData();
    formData.append('file', selectedFile);

    try {
        console.log('📦 Создаю полный 3D пакет...');
        const response = await fetch('/api/export-3d/package', {
            method: 'POST',
            body: formData,
            headers: {
                'Accept': 'application/json'
            }
        });

        const data = await response.json();

        if (response.ok && data.success) {
            console.log('✓ Пакет создан', data);
            
            let statsMsg = `✓ 3D Пакет создан!\n\n`;
            statsMsg += `� Сеток: ${data.statistics.meshes}\n`;
            statsMsg += `⬜️ Вершин: ${data.statistics.vertices.toLocaleString()}\n`;
            statsMsg += `▲ Треугольников: ${data.statistics.triangles.toLocaleString()}\n`;
            statsMsg += `📦 Платформ: ${data.statistics.platforms}\n`;
            statsMsg += `⚠️ Препятствий: ${data.statistics.obstacles}\n\n`;
            statsMsg += `📄 Файлов в пакете: ${data.statistics.total_files}\n`;
            statsMsg += `Форматы: ${data.files.join(', ')}\n`;
            statsMsg += `Размер: ~${(data.package_size_estimate / 1024).toFixed(2)} KB`;
            
            alert(statsMsg);
        } else {
            showError(data.error || 'Ошибка при создании пакета');
        }
    } catch (error) {
        console.error('✗ Ошибка пакета:', error);
        showError('Ошибка создания пакета: ' + error.message);
    } finally {
        loadingSpinner.style.display = 'none';
    }
}

function downloadFile(content, filename, mimeType) {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    console.log(`✓ Загружен: ${filename}`);
}

generateBtn.addEventListener('click', async () => {
    if (!selectedFile) {
        showError('Пожалуйста, выберите изображение');
        return;
    }

    loadingSpinner.style.display = 'block';
    resultCard.style.display = 'none';
    errorCard.style.display = 'none';
    generateBtn.disabled = true;

    const formData = new FormData();
    formData.append('file', selectedFile);

    try {
        console.log('🎨 Генерирую 3D модель от изображения:', selectedFile.name);
        const response = await fetch('/api/generate', {
            method: 'POST',
            body: formData,
            headers: {
                'Accept': 'application/json'
            }
        });

        const data = await response.json();

        console.log('✓ Ответ от сервера:', data);

        if (response.ok && data.success) {
            await displayResult(data);
        } else {
            showError(data.error || 'Ошибка при генерации 3D модели');
        }
    } catch (error) {
        console.error('✗ Ошибка:', error);
        showError('Ошибка соединения с сервером: ' + error.message);
    } finally {
        loadingSpinner.style.display = 'none';
        generateBtn.disabled = false;
    }
});

async function displayResult(data) {
    generatedData = data;

    // Ensure Viewer3D class is available before proceeding
    await ensureViewer3D();

    // Display 3D geometry statistics
    const stats = data.geometry_stats;
    if (stats) {
        document.getElementById('meshCount').textContent = stats.total_meshes;
        document.getElementById('vertexCount').textContent = stats.total_vertices.toLocaleString();
        document.getElementById('triangleCount').textContent = stats.total_triangles.toLocaleString();
        document.getElementById('platformCount').textContent = stats.detected_platforms;
        document.getElementById('objectCount').textContent = data.objects_detected;
        
        // Format volume (scene_volume is typically cubic units)
        const volumeText = stats.scene_volume > 0 
            ? stats.scene_volume.toFixed(2) + ' m³'
            : 'N/A';
        document.getElementById('volumeSize').textContent = volumeText;
    }

    // Display version and generation info
    document.getElementById('statusBadge').textContent = '✓ ' + data.version;

    // Store analysis for later use
    if (data.analysis) {
        sessionStorage.setItem('current3DAnalysis', JSON.stringify(data.analysis));
    }

    const jsonStr = JSON.stringify(data, null, 2);
    document.getElementById('jsonPreview').textContent = jsonStr;
    document.getElementById('jsonPreview').style.maxHeight = '400px';
    document.getElementById('jsonPreview').style.overflowY = 'auto';

    // Initialize 3D Viewer with error handling
    try {
        // Check if Three.js is available
        if (typeof THREE === 'undefined') {
            throw new Error('Three.js library is not loaded');
        }

        // Check if OrbitControls is available
        if (typeof THREE.OrbitControls === 'undefined') {
            throw new Error('Three.js OrbitControls is not loaded');
        }

        // Check if Viewer3D class is available
        if (typeof Viewer3D === 'undefined') {
            throw new Error('Viewer3D class is not defined. Check browser console for viewer-3d.js errors.');
        }

        console.log('✓ Three.js libraries loaded successfully');
        console.log('THREE:', typeof THREE);
        console.log('THREE.OrbitControls:', typeof THREE.OrbitControls);
        console.log('Viewer3D class:', typeof Viewer3D);

        // The 3D container must be visible BEFORE the viewer initializes, otherwise it sizes
        // its WebGL canvas to 0×0 (the result card was display:none) and nothing renders.
        resultCard.style.display = 'block';

        if (!viewer3D) {
            console.log('🔧 Initializing 3D Viewer...');
            viewer3D = new Viewer3D('canvas3d');
            window.__viewer3D = viewer3D; // expose for debugging/automated checks
            console.log('✓ 3D Viewer инициализирован');
        }

        if (viewer3D && typeof viewer3D.setSourceImage === 'function' && previewDataUrl) {
            viewer3D.setSourceImage(previewDataUrl);
        }

        if (viewer3D && typeof viewer3D.loadAnalysisData === 'function') {
            viewer3D.loadAnalysisData(data);
            // Ensure the renderer matches the now-visible container size.
            if (typeof viewer3D.onWindowResize === 'function') viewer3D.onWindowResize();
            console.log('✓ Analysis data loaded to viewer');
        } else {
            throw new Error('Viewer3D.loadAnalysisData is not a function');
        }

        worldInitBtn.disabled = false;
    } catch (error) {
        console.error('❌ Error initializing Viewer3D:', error);
        console.error('Error stack:', error.stack);
        showError('Ошибка при инициализации 3D просмотра: ' + error.message + '\n\nПожалуйста, проверьте консоль браузера (F12) для деталей.');
        return;
    }

    // Enable 3D control buttons
    document.getElementById('autoRotateBtn').disabled = false;
    document.getElementById('resetCameraBtn').disabled = false;

    resultCard.style.display = 'block';
    resultCard.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

async function initWorldSession() {
    if (!selectedFile) {
        showError('Пожалуйста, выберите изображение');
        return;
    }

    const formData = new FormData();
    formData.append('file', selectedFile);

    try {
        console.log('🧠 Init world session (Yan-style)...');
        const response = await fetch('/api/world/init', {
            method: 'POST',
            body: formData,
            headers: { 'Accept': 'application/json' }
        });

        const data = await response.json();
        if (!response.ok || !data.success) {
            throw new Error(data.error || 'World init failed');
        }

        worldSessionId = data.session_id;
        console.log('✓ World session created:', worldSessionId);
        if (viewer3D && typeof viewer3D.setSourceImage === 'function' && previewDataUrl) {
            viewer3D.setSourceImage(previewDataUrl);
        }
        updateWorldView(data.world_state);

        worldStepForwardBtn.disabled = false;
        worldTurnLeftBtn.disabled = false;
        worldTurnRightBtn.disabled = false;
        worldApplyStyleBtn.disabled = false;
        worldAddObjectBtn.disabled = false;
        worldSaveBtn.disabled = false;
        worldSessionInput.value = worldSessionId;
    } catch (error) {
        console.error('✗ World init error:', error);
        showError('Ошибка инициализации World Model: ' + error.message);
    }
}

async function stepWorld(action) {
    if (!worldSessionId) return;

    try {
        const response = await fetch('/api/world/step', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
            body: JSON.stringify({ session_id: worldSessionId, action })
        });
        const data = await response.json();
        if (!response.ok || !data.success) {
            throw new Error(data.error || 'World step failed');
        }
        updateWorldView(data.world_state);
    } catch (error) {
        console.error('✗ World step error:', error);
        showError('Ошибка шага симуляции: ' + error.message);
    }
}

async function editWorld(edit) {
    if (!worldSessionId) return;

    try {
        const response = await fetch('/api/world/edit', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
            body: JSON.stringify({ session_id: worldSessionId, edit })
        });
        const data = await response.json();
        if (!response.ok || !data.success) {
            throw new Error(data.error || 'World edit failed');
        }
        updateWorldView(data.world_state);
    } catch (error) {
        console.error('✗ World edit error:', error);
        showError('Ошибка редактирования World Model: ' + error.message);
    }
}

async function saveWorldSession() {
    if (!worldSessionId) return;

    try {
        const response = await fetch('/api/world/save', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
            body: JSON.stringify({ session_id: worldSessionId })
        });
        const data = await response.json();
        if (!response.ok || !data.success) {
            throw new Error(data.error || 'World save failed');
        }
        console.log('✓ World session saved:', data.file);
    } catch (error) {
        console.error('✗ World save error:', error);
        showError('Ошибка сохранения сессии: ' + error.message);
    }
}

async function loadWorldSession() {
    const sessionId = (worldSessionInput.value || '').trim();
    if (!sessionId) {
        showError('Введите Session ID');
        return;
    }

    try {
        const response = await fetch('/api/world/load', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
            body: JSON.stringify({ session_id: sessionId })
        });
        const data = await response.json();
        if (!response.ok || !data.success) {
            throw new Error(data.error || 'World load failed');
        }

        worldSessionId = data.session_id;
        worldSessionInput.value = worldSessionId;
        updateWorldView(data.world_state);
        worldStepForwardBtn.disabled = false;
        worldTurnLeftBtn.disabled = false;
        worldTurnRightBtn.disabled = false;
        worldApplyStyleBtn.disabled = false;
        worldAddObjectBtn.disabled = false;
        worldSaveBtn.disabled = false;
    } catch (error) {
        console.error('✗ World load error:', error);
        showError('Ошибка загрузки сессии: ' + error.message);
    }
}

function updateWorldView(worldState) {
    if (!viewer3D) return;
    if (typeof viewer3D.loadWorldState === 'function') {
        viewer3D.loadWorldState(worldState);
    } else if (typeof viewer3D.loadAnalysisData === 'function') {
        viewer3D.loadAnalysisData({ analysis: { detailed_objects: worldState.Objects || [] } });
    }
}

function showError(message) {
    document.getElementById('errorMessage').textContent = message;
    errorCard.style.display = 'block';
    console.error('✗ Ошибка:', message);
}

downloadBtn.addEventListener('click', () => {
    if (!generatedData) return;

    const report = generatedData.generation_report || generatedData.data;
    const jsonBlob = new Blob(
        [JSON.stringify(report, null, 2)],
        { type: 'application/json' }
    );

    const url = URL.createObjectURL(jsonBlob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'level_report.json';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
});

// 3D Viewer Control Buttons
document.getElementById('autoRotateBtn').addEventListener('click', () => {
    if (viewer3D) {
        viewer3D.toggleAutoRotate();
    }
});

document.getElementById('resetCameraBtn').addEventListener('click', () => {
    if (viewer3D) {
        viewer3D.resetCamera();
    }
});

worldInitBtn.addEventListener('click', initWorldSession);
worldStepForwardBtn.addEventListener('click', () => stepWorld('move_forward'));
worldTurnLeftBtn.addEventListener('click', () => stepWorld('turn_left'));
worldTurnRightBtn.addEventListener('click', () => stepWorld('turn_right'));
worldApplyStyleBtn.addEventListener('click', () => {
    const style = worldStyleSelect.value;
    editWorld({ edit_type: 'style', style });
});
worldAddObjectBtn.addEventListener('click', () => {
    editWorld({ edit_type: 'add_object', object_type: 'platform', x: 0.5, y: 0.5, width: 0.08, height: 0.08 });
});
worldSaveBtn.addEventListener('click', saveWorldSession);
worldLoadBtn.addEventListener('click', loadWorldSession);

// Listen for object selection in 3D viewer
document.addEventListener('meshSelected', (event) => {
    const details = event.detail;
    const objectPanel = document.getElementById('objectPanel');
    
    document.getElementById('objType').textContent = details.type || 'Unknown';
    document.getElementById('objIndex').textContent = details.index || '—';
    document.getElementById('objPos').textContent = 
        `(${details.position.x.toFixed(0)}, ${details.position.y.toFixed(0)}, ${details.position.z.toFixed(0)})`;
    document.getElementById('objColor').textContent = '#' + details.material.color;
    
    objectPanel.style.display = 'block';
});

// Check API health on load
window.addEventListener('load', async () => {
    try {
        const response = await fetch('/api/health');
        const data = await response.json();
        console.log('✓ Сервер онлайн:', data);
        document.getElementById('statusBadge').textContent = `✓ ${data.version}`;
    } catch (error) {
        console.error('✗ Сервер не отвечает:', error);
        showError('Сервер недоступен');
        document.getElementById('statusBadge').textContent = '✗ Offline';
    }
});
