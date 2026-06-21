/**
 * 3D Level Viewer - Enhanced
 * ============================
 * Визуализация 3D уровня с высокой детализацией
 */

console.log('📦 [viewer-3d.js] Starting to load...');

// Ensure THREE and OrbitControls are available
if (typeof THREE === 'undefined') {
    console.error('❌ [viewer-3d.js] THREE.js NOT loaded! Waiting...');
    // Wait a bit for THREE to load
    setTimeout(() => {
        if (typeof THREE === 'undefined') {
            console.error('❌ [viewer-3d.js] THREE.js STILL not available after timeout');
        } else {
            console.log('✓ [viewer-3d.js] THREE.js loaded after wait');
        }
    }, 500);
}

/**
 * 3D Level Viewer
 * ================
 * Визуализация сгенерированного 3D уровня с WebGL
 * - Интерактивная сцена Three.js
 * - Отдельная демонстрация объектов
 * - Управление камерой и материалами
 * - Текстурирование и освещение
 */

// Wrap entire Viewer3D in IIFE to ensure THREE is loaded
(function() {
    'use strict';

    // Wait for THREE.js to be loaded
    if (typeof THREE === 'undefined') {
        console.error('❌ FATAL: Three.js is not loaded when viewer-3d.js executed!');
        console.error('Available globals:', Object.keys(window).filter(k => k.includes('THREE') || k.includes('Orbit')));
        return;
    }

    console.log('✓ Three.js loaded, version:', THREE.REVISION);
    console.log('✓ THREE.OrbitControls available:', typeof THREE.OrbitControls !== 'undefined');

    window.Viewer3D = class Viewer3D {
        constructor(containerId) {
            try {
                // Verify THREE is still available
                if (typeof THREE === 'undefined') {
                    throw new Error('THREE is not available in Viewer3D constructor');
                }

                if (typeof THREE.OrbitControls === 'undefined') {
                    throw new Error('THREE.OrbitControls is not available. Make sure OrbitControls.js is loaded');
                }

                if (!document.getElementById(containerId)) {
                    throw new Error(`Container with id "${containerId}" not found in DOM`);
                }

                this.container = document.getElementById(containerId);
                this.scene = new THREE.Scene();
                this.camera = null;
                this.renderer = null;
                this.controls = null;
                this.raycaster = null;
                this.mouse = null;
                this.meshes = [];
                this.selectedMesh = null;
                this.analysisData = null;
                this.isInitialized = false;
                this.textureCache = {};
                this.sourceImageUrl = null;
                this.sourceImageTexture = null;
                this.sourceImageData = null;
                this.sourceImageWidth = 0;
                this.sourceImageHeight = 0;
                this.sourceEdgeMap = null;
                this.deferredAnalysisData = null;

                console.log('✓ Viewer3D: Инициализация сцены...');
                this.initScene();
                
                console.log('✓ Viewer3D: Добавление освещения...');
                this.setupLighting();
                
                console.log('✓ Viewer3D: Инициализация управления камерой...');
                this.setupControls();
                
                console.log('✓ Viewer3D: Установка размера окна...');
                this.onWindowResize();
                
                window.addEventListener('resize', () => this.onWindowResize());
                
                this.isInitialized = true;
                console.log('✓ Viewer3D: Инициализация завершена успешно');
                
                // Start render loop AFTER all initialization is complete
                this.animate();
            } catch (error) {
                console.error('❌ Viewer3D init error:', error);
                console.error('Error stack:', error.stack);
                this.handleInitationError(error);
            }
        }

        handleInitationError(error) {
            const errorDiv = document.createElement('div');
            errorDiv.style.cssText = `
                background: #ff4444;
                color: white;
                padding: 20px;
                margin: 10px;
                border-radius: 5px;
                font-family: monospace;
                white-space: pre-wrap;
                word-break: break-all;
            `;
            errorDiv.textContent = `3D Viewer Init Error:\n${error.message}\n\nstack: ${error.stack}`;
            if (this.container) {
                this.container.innerHTML = '';
                this.container.appendChild(errorDiv);
            }
        }

    initScene() {
        // Scene setup — realistic sky + atmospheric fog for depth
        this.skyColor = new THREE.Color(0x9fc4e8);
        this.scene.background = this.createSkyTexture();
        this.scene.fog = new THREE.Fog(0xbcd4ec, 1600, 4200);

        // Camera
        this.camera = new THREE.PerspectiveCamera(
            60,
            this.container.clientWidth / this.container.clientHeight,
            0.1,
            12000
        );
        this.camera.position.set(200, 150, 250);
        this.camera.lookAt(0, 0, 0);

        // Renderer
        this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
        this.renderer.setSize(this.container.clientWidth, this.container.clientHeight);
        this.renderer.setPixelRatio(Math.min(2, window.devicePixelRatio));
        this.renderer.shadowMap.enabled = true;
        this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
        this.renderer.toneMappingExposure = 1.05;
        this.renderer.outputEncoding = THREE.sRGBEncoding;
        this.container.appendChild(this.renderer.domElement);

        // Debug helpers (grid + axes) — hidden by default so the photo terrain looks realistic.
        // Toggle with setHelpersVisible(true) if you need orientation aids.
        this.gridHelper = new THREE.GridHelper(1600, 32, 0x52708f, 0x33485c);
        this.gridHelper.position.y = -50;
        this.gridHelper.visible = false;
        this.scene.add(this.gridHelper);

        this.axesHelper = new THREE.AxesHelper(100);
        this.axesHelper.visible = false;
        this.scene.add(this.axesHelper);
    }

    setHelpersVisible(visible) {
        if (this.gridHelper) this.gridHelper.visible = !!visible;
        if (this.axesHelper) this.axesHelper.visible = !!visible;
    }

    // Vertical gradient backdrop that reads as a daytime sky.
    createSkyTexture() {
        const canvas = document.createElement('canvas');
        canvas.width = 16;
        canvas.height = 256;
        const ctx = canvas.getContext('2d');
        const grad = ctx.createLinearGradient(0, 0, 0, 256);
        grad.addColorStop(0.0, '#6fa8dc');   // zenith
        grad.addColorStop(0.55, '#a9cdeb');
        grad.addColorStop(1.0, '#e7f0f7');   // horizon haze
        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, 16, 256);
        const texture = new THREE.CanvasTexture(canvas);
        texture.encoding = THREE.sRGBEncoding;
        texture.needsUpdate = true;
        return texture;
    }

    setupLighting() {
        // Sky/ground fill — soft, directionless base light (keeps shadows from going pure black).
        const hemiLight = new THREE.HemisphereLight(0xbfd8ff, 0x55503f, 0.55);
        hemiLight.position.set(0, 600, 0);
        this.scene.add(hemiLight);

        // Low ambient so the directional "sun" can actually sculpt the relief.
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.25);
        this.scene.add(ambientLight);

        // Sun — the main light. Warm, strong, casts shadows that reveal the terrain relief.
        const sun = new THREE.DirectionalLight(0xfff1da, 2.1);
        sun.position.set(420, 640, 320);
        sun.castShadow = true;
        sun.shadow.mapSize.width = 2048;
        sun.shadow.mapSize.height = 2048;
        // Cover the full photo terrain (worldRange ≈ 1400, half-extent 700 + margin).
        sun.shadow.camera.left = -1000;
        sun.shadow.camera.right = 1000;
        sun.shadow.camera.top = 1000;
        sun.shadow.camera.bottom = -1000;
        sun.shadow.camera.near = 1;
        sun.shadow.camera.far = 3000;
        sun.shadow.bias = -0.0004;
        sun.shadow.normalBias = 0.6;
        this.scene.add(sun);
        this.sun = sun;
    }

    setupControls() {
        // OrbitControls для интерактивного управления камерой
        if (!THREE.OrbitControls) {
            console.error('❌ THREE.OrbitControls is not loaded');
            console.error('window.THREE:', window.THREE);
            console.error('Available THREE properties:', Object.keys(window.THREE || {}).slice(0, 20));
            throw new Error('THREE.OrbitControls not available');
        }

        try {
            this.controls = new THREE.OrbitControls(this.camera, this.renderer.domElement);
            this.controls.enableDamping = true;
            this.controls.dampingFactor = 0.05;
            this.controls.autoRotate = true;
            this.controls.autoRotateSpeed = 2;
            this.controls.enableZoom = true;
            this.controls.enablePan = true;
            console.log('✓ OrbitControls инициализированы');
        } catch (e) {
            console.error('❌ OrbitControls initialization failed:', e);
            throw e;
        }

        // Raycaster für object selection
        this.raycaster = new THREE.Raycaster();
        this.mouse = new THREE.Vector2();
        this.renderer.domElement.addEventListener('click', (e) => this.onMeshClick(e));
    }

    onWindowResize() {
        const width = this.container.clientWidth;
        const height = this.container.clientHeight;
        this.camera.aspect = width / height;
        this.camera.updateProjectionMatrix();
        this.renderer.setSize(width, height);
    }

    createProceduralTexture(type, colorHex) {
        const key = `${type}-${colorHex}`;
        if (this.textureCache[key]) {
            return this.textureCache[key];
        }

        const canvas = document.createElement('canvas');
        canvas.width = 128;
        canvas.height = 128;
        const ctx = canvas.getContext('2d');
        const base = new THREE.Color(colorHex);
        const c1 = `rgb(${Math.floor(base.r * 255)}, ${Math.floor(base.g * 255)}, ${Math.floor(base.b * 255)})`;
        const c2 = `rgb(${Math.floor(Math.min(255, base.r * 255 + 28))}, ${Math.floor(Math.min(255, base.g * 255 + 28))}, ${Math.floor(Math.min(255, base.b * 255 + 28))})`;
        const c3 = `rgb(${Math.floor(Math.max(0, base.r * 255 - 35))}, ${Math.floor(Math.max(0, base.g * 255 - 35))}, ${Math.floor(Math.max(0, base.b * 255 - 35))})`;

        ctx.fillStyle = c1;
        ctx.fillRect(0, 0, 128, 128);

        if (type === 'platform') {
            for (let i = 0; i < 128; i += 16) {
                ctx.fillStyle = i % 32 === 0 ? c2 : c3;
                ctx.fillRect(0, i, 128, 8);
            }
        } else if (type === 'obstacle') {
            for (let y = 0; y < 128; y += 20) {
                for (let x = 0; x < 128; x += 20) {
                    ctx.fillStyle = ((x + y) / 20) % 2 === 0 ? c2 : c3;
                    ctx.fillRect(x, y, 18, 18);
                }
            }
        } else if (type === 'enemy') {
            for (let i = 0; i < 128; i += 12) {
                ctx.strokeStyle = i % 24 === 0 ? c2 : c3;
                ctx.beginPath();
                ctx.moveTo(i, 0);
                ctx.lineTo(0, i);
                ctx.stroke();
            }
        } else if (type === 'collectible') {
            ctx.fillStyle = c2;
            for (let i = 0; i < 18; i++) {
                const x = Math.floor(Math.random() * 128);
                const y = Math.floor(Math.random() * 128);
                ctx.beginPath();
                ctx.arc(x, y, 3 + (i % 3), 0, Math.PI * 2);
                ctx.fill();
            }
        }

        const texture = new THREE.CanvasTexture(canvas);
        texture.wrapS = THREE.RepeatWrapping;
        texture.wrapT = THREE.RepeatWrapping;
        texture.repeat.set(2, 2);
        texture.anisotropy = 4;
        texture.needsUpdate = true;
        this.textureCache[key] = texture;
        return texture;
    }

    createMaterial(type = 'standard', color = null) {
        const palette = {
            platform: 0x4a90e2,
            enemy: 0xe74c3c,
            collectible: 0xf1c40f,
            obstacle: 0x95a5a6,
            selected: 0x00ff00
        };

        const materialConfig = {
            platform: { metalness: 0.3, roughness: 0.72, emissive: 0x1a3a52 },
            enemy: { metalness: 0.45, roughness: 0.5, emissive: 0x5f1a1a },
            collectible: { metalness: 0.85, roughness: 0.2, emissive: 0x6e5b00 },
            obstacle: { metalness: 0.35, roughness: 0.62, emissive: 0x2f3133 },
            selected: { metalness: 0.9, roughness: 0.1, emissive: 0x00aa00, wireframe: false }
        };

        const resolvedType = materialConfig[type] ? type : 'platform';
        const baseColor = color || palette[resolvedType] || palette.platform;
        const texture = this.createProceduralTexture(resolvedType, baseColor);
        return new THREE.MeshStandardMaterial({
            color: baseColor,
            map: texture,
            ...materialConfig[resolvedType]
        });
    }

    createSolidMaterial(type = 'standard', color = null) {
        const palette = {
            platform: 0x4a90e2,
            enemy: 0xe74c3c,
            collectible: 0xf1c40f,
            obstacle: 0x95a5a6,
            selected: 0x00ff00
        };

        const materialConfig = {
            platform: { metalness: 0.2, roughness: 0.8, emissive: 0x000000 },
            enemy: { metalness: 0.3, roughness: 0.6, emissive: 0x000000 },
            collectible: { metalness: 0.5, roughness: 0.4, emissive: 0x000000 },
            obstacle: { metalness: 0.25, roughness: 0.75, emissive: 0x000000 },
            selected: { metalness: 0.9, roughness: 0.1, emissive: 0x00aa00, wireframe: false }
        };

        const resolvedType = materialConfig[type] ? type : 'platform';
        const baseColor = color || palette[resolvedType] || palette.platform;
        return new THREE.MeshStandardMaterial({
            color: baseColor,
            ...materialConfig[resolvedType]
        });
    }

    createPhotoMaterial(type = 'platform', fallbackColor = null) {
        if (!this.sourceImageTexture) {
            return this.createMaterial(type, fallbackColor);
        }

        const baseColor = fallbackColor || 0xffffff;
        return new THREE.MeshStandardMaterial({
            color: baseColor,
            map: this.sourceImageTexture,
            roughness: type === 'collectible' ? 0.6 : 0.85,
            metalness: 0.05
        });
    }

    getObjectNumber(obj, lowerKey, upperKey, fallback) {
        const raw = obj?.[lowerKey] ?? obj?.[upperKey] ?? fallback;
        const val = Number(raw);
        return Number.isFinite(val) ? val : fallback;
    }

    getObjectString(obj, lowerKey, upperKey, fallback) {
        const raw = obj?.[lowerKey] ?? obj?.[upperKey] ?? fallback;
        return (raw ?? fallback).toString();
    }

    getObjectBrightness(obj) {
        const b = this.getObjectNumber(obj, 'brightness', 'Brightness', 140);
        return Math.min(255, Math.max(0, b));
    }

    createStructureGrid(objects, minX, spanX, minY, spanY, gridSize = 26) {
        const cells = Array.from({ length: gridSize * gridSize }, () => ({
            count: 0,
            brightnessSum: 0,
            typeVotes: { platform: 0, enemy: 0, collectible: 0, obstacle: 0 }
        }));

        const clamp01 = (v) => Math.max(0, Math.min(1, v));
        const toIndex = (gx, gz) => gz * gridSize + gx;

        objects.forEach(obj => {
            const x = this.getObjectNumber(obj, 'x', 'X', 0);
            const y = this.getObjectNumber(obj, 'y', 'Y', 0);
            const w = Math.max(1, this.getObjectNumber(obj, 'width', 'Width', 20));
            const h = Math.max(1, this.getObjectNumber(obj, 'height', 'Height', 20));
            const type = this.normalizeObjectType(this.getObjectString(obj, 'type', 'Type', 'obstacle'));
            const brightness = this.getObjectBrightness(obj);

            const u0 = clamp01((x - minX) / spanX);
            const v0 = clamp01((y - minY) / spanY);
            const u1 = clamp01((x + w - minX) / spanX);
            const v1 = clamp01((y + h - minY) / spanY);

            const gx0 = Math.max(0, Math.floor(u0 * (gridSize - 1)));
            const gz0 = Math.max(0, Math.floor(v0 * (gridSize - 1)));
            const gx1 = Math.min(gridSize - 1, Math.ceil(u1 * (gridSize - 1)));
            const gz1 = Math.min(gridSize - 1, Math.ceil(v1 * (gridSize - 1)));

            for (let gz = gz0; gz <= gz1; gz++) {
                for (let gx = gx0; gx <= gx1; gx++) {
                    const cell = cells[toIndex(gx, gz)];
                    cell.count += 1;
                    cell.brightnessSum += brightness;
                    cell.typeVotes[type] = (cell.typeVotes[type] || 0) + 1;
                }
            }
        });

        return { cells, gridSize };
    }

    addStructuredTerrain(structureGrid, worldRange) {
        const { cells, gridSize } = structureGrid;
        const cellSize = worldRange / gridSize;
        const toIndex = (gx, gz) => gz * gridSize + gx;

        const getDominantType = (votes) => {
            let bestType = 'platform';
            let bestVote = -1;
            Object.entries(votes).forEach(([t, v]) => {
                if (v > bestVote) {
                    bestVote = v;
                    bestType = t;
                }
            });
            return bestType;
        };

        const normalized = Array.from({ length: gridSize * gridSize }, (_, idx) => {
            const cell = cells[idx];
            if (cell.count <= 0) {
                return { occupied: false, type: 'platform', height: 0 };
            }
            const avgBrightness = cell.brightnessSum / cell.count;
            const terrainHeight = 8 + (avgBrightness / 255) * 18 + Math.min(10, cell.count * 0.9);
            return {
                occupied: true,
                type: getDominantType(cell.typeVotes),
                height: terrainHeight
            };
        });

        // Smooth heights with a 3x3 neighborhood average to reduce staircase artifacts.
        const smoothed = normalized.map((cell, idx) => {
            if (!cell.occupied) return cell;
            const gx = idx % gridSize;
            const gz = Math.floor(idx / gridSize);
            let sum = 0;
            let wsum = 0;
            for (let dz = -1; dz <= 1; dz++) {
                for (let dx = -1; dx <= 1; dx++) {
                    const nx = gx + dx;
                    const nz = gz + dz;
                    if (nx < 0 || nz < 0 || nx >= gridSize || nz >= gridSize) continue;
                    const n = normalized[toIndex(nx, nz)];
                    if (!n.occupied) continue;
                    const w = (dx === 0 && dz === 0) ? 3 : 1;
                    sum += n.height * w;
                    wsum += w;
                }
            }
            const h = wsum > 0 ? sum / wsum : cell.height;
            return { ...cell, height: h };
        });

        const occupied = (gx, gz) => {
            if (gx < 0 || gz < 0 || gx >= gridSize || gz >= gridSize) return false;
            return smoothed[toIndex(gx, gz)].occupied;
        };

        const terrainOf = (gx, gz) => smoothed[toIndex(gx, gz)];

        // Merge similar cells into rectangles to reduce staircase artifacts.
        const visited = Array.from({ length: gridSize * gridSize }, () => false);
        const heightThreshold = 4.5;

        const canMerge = (gx, gz, base) => {
            if (gx < 0 || gz < 0 || gx >= gridSize || gz >= gridSize) return false;
            const cell = terrainOf(gx, gz);
            if (!cell.occupied) return false;
            if (cell.type !== base.type) return false;
            return Math.abs(cell.height - base.height) <= heightThreshold;
        };

        for (let gz = 0; gz < gridSize; gz++) {
            for (let gx = 0; gx < gridSize; gx++) {
                const idx = toIndex(gx, gz);
                const base = terrainOf(gx, gz);
                if (!base.occupied || visited[idx]) continue;

                // Expand width.
                let maxW = 1;
                while (gx + maxW < gridSize && canMerge(gx + maxW, gz, base) && !visited[toIndex(gx + maxW, gz)]) {
                    maxW++;
                }

                // Expand height while all cells in the row are mergeable.
                let maxH = 1;
                outer: while (gz + maxH < gridSize) {
                    for (let dx = 0; dx < maxW; dx++) {
                        if (!canMerge(gx + dx, gz + maxH, base)) break outer;
                        if (visited[toIndex(gx + dx, gz + maxH)]) break outer;
                    }
                    maxH++;
                }

                let heightSum = 0;
                let count = 0;
                for (let dz = 0; dz < maxH; dz++) {
                    for (let dx = 0; dx < maxW; dx++) {
                        const cell = terrainOf(gx + dx, gz + dz);
                        heightSum += cell.height;
                        count++;
                        visited[toIndex(gx + dx, gz + dz)] = true;
                    }
                }

                const avgHeight = heightSum / Math.max(1, count);
                const width = maxW * cellSize * 0.94;
                const depth = maxH * cellSize * 0.94;
                const x = (gx + maxW / 2) * cellSize - worldRange / 2;
                const z = (gz + maxH / 2) * cellSize - worldRange / 2;
                const y = -50 + avgHeight / 2;

                const terrainMesh = new THREE.Mesh(
                    new THREE.BoxGeometry(width, avgHeight, depth),
                    this.createMaterial(base.type)
                );
                terrainMesh.position.set(x, y, z);
                terrainMesh.castShadow = true;
                terrainMesh.receiveShadow = true;
                terrainMesh.userData = {
                    type: 'terrain',
                    variant: `${base.type}-terrain-rect`,
                    index: gx + gz * gridSize,
                    merged_cells: count
                };
                this.scene.add(terrainMesh);
                this.meshes.push(terrainMesh);
            }
        }

        // Extrude boundary walls from smoothed terrain grid.
        for (let gz = 0; gz < gridSize; gz++) {
            for (let gx = 0; gx < gridSize; gx++) {
                const cell = terrainOf(gx, gz);
                if (!cell.occupied) continue;

                const x = (gx + 0.5) * cellSize - worldRange / 2;
                const z = (gz + 0.5) * cellSize - worldRange / 2;
                const wallHeight = cell.height + 22;
                const wallThickness = Math.max(4, cellSize * 0.14);
                const wallY = -50 + wallHeight / 2;

                if (!occupied(gx + 1, gz)) {
                    const wall = new THREE.Mesh(
                        new THREE.BoxGeometry(wallThickness, wallHeight, cellSize * 0.92),
                        this.createMaterial('obstacle', 0x8f96a3)
                    );
                    wall.position.set(x + cellSize * 0.48, wallY, z);
                    wall.castShadow = true;
                    wall.receiveShadow = true;
                    wall.userData = { type: 'wall', variant: 'east-wall', index: gx + gz * gridSize };
                    this.scene.add(wall);
                    this.meshes.push(wall);
                }

                if (!occupied(gx - 1, gz)) {
                    const wall = new THREE.Mesh(
                        new THREE.BoxGeometry(wallThickness, wallHeight, cellSize * 0.92),
                        this.createMaterial('obstacle', 0x8f96a3)
                    );
                    wall.position.set(x - cellSize * 0.48, wallY, z);
                    wall.castShadow = true;
                    wall.receiveShadow = true;
                    wall.userData = { type: 'wall', variant: 'west-wall', index: gx + gz * gridSize };
                    this.scene.add(wall);
                    this.meshes.push(wall);
                }

                if (!occupied(gx, gz + 1)) {
                    const wall = new THREE.Mesh(
                        new THREE.BoxGeometry(cellSize * 0.92, wallHeight, wallThickness),
                        this.createMaterial('obstacle', 0x8f96a3)
                    );
                    wall.position.set(x, wallY, z + cellSize * 0.48);
                    wall.castShadow = true;
                    wall.receiveShadow = true;
                    wall.userData = { type: 'wall', variant: 'south-wall', index: gx + gz * gridSize };
                    this.scene.add(wall);
                    this.meshes.push(wall);
                }

                if (!occupied(gx, gz - 1)) {
                    const wall = new THREE.Mesh(
                        new THREE.BoxGeometry(cellSize * 0.92, wallHeight, wallThickness),
                        this.createMaterial('obstacle', 0x8f96a3)
                    );
                    wall.position.set(x, wallY, z - cellSize * 0.48);
                    wall.castShadow = true;
                    wall.receiveShadow = true;
                    wall.userData = { type: 'wall', variant: 'north-wall', index: gx + gz * gridSize };
                    this.scene.add(wall);
                    this.meshes.push(wall);
                }

                // Height transition skirts for smoother steps between neighboring cells.
                const addSkirt = (dirX, dirZ) => {
                    const n = terrainOf(gx + dirX, gz + dirZ);
                    if (!n || !n.occupied) return;
                    const diff = Math.abs(cell.height - n.height);
                    if (diff < 6) return;

                    const skirtHeight = Math.min(18, diff * 0.6);
                    const skirtThickness = Math.max(4, cellSize * 0.18);
                    const skirtLength = cellSize * 0.92;
                    const skirtY = -50 + Math.min(cell.height, n.height) + diff * 0.5;
                    const mat = this.createMaterial('platform', 0xb6c2d4);

                    const skirt = new THREE.Mesh(
                        new THREE.BoxGeometry(
                            dirX !== 0 ? skirtThickness : skirtLength,
                            skirtHeight,
                            dirZ !== 0 ? skirtThickness : skirtLength
                        ),
                        mat
                    );
                    skirt.position.set(
                        x + dirX * cellSize * 0.48,
                        skirtY,
                        z + dirZ * cellSize * 0.48
                    );
                    skirt.castShadow = true;
                    skirt.receiveShadow = true;
                    skirt.userData = { type: 'transition', variant: 'height-skirt', index: gx + gz * gridSize };
                    this.scene.add(skirt);
                    this.meshes.push(skirt);
                };

                addSkirt(1, 0);
                addSkirt(-1, 0);
                addSkirt(0, 1);
                addSkirt(0, -1);
            }
        }
    }

    colorFromObject(obj, fallbackType) {
        const r = this.getObjectNumber(obj, 'color_r', 'Color_R', -1);
        const g = this.getObjectNumber(obj, 'color_g', 'Color_G', -1);
        const b = this.getObjectNumber(obj, 'color_b', 'Color_B', -1);
        if (r >= 0 && g >= 0 && b >= 0) {
            // Lift very dark colors so geometry remains visible on dark backgrounds.
            const minChannel = 48;
            const rr = Math.max(minChannel, Math.min(255, r));
            const gg = Math.max(minChannel, Math.min(255, g));
            const bb = Math.max(minChannel, Math.min(255, b));
            return new THREE.Color(rr / 255, gg / 255, bb / 255).getHex();
        }
        return this.createMaterial(fallbackType).color.getHex();
    }

    addVisibilityMarkers(center = new THREE.Vector3(0, 0, 0)) {
        const markerGeo = new THREE.SphereGeometry(10, 20, 20);
        const markerMat = new THREE.MeshStandardMaterial({
            color: 0xff4d6d,
            emissive: 0xaa2038,
            emissiveIntensity: 0.8,
            metalness: 0.2,
            roughness: 0.4
        });
        const marker = new THREE.Mesh(markerGeo, markerMat);
        marker.position.copy(center);
        marker.position.y += 14;
        marker.castShadow = true;
        marker.receiveShadow = true;
        marker.userData = { type: 'marker', variant: 'center-marker', index: -1 };
        this.scene.add(marker);
        this.meshes.push(marker);

        const ringGeo = new THREE.TorusGeometry(24, 2.4, 16, 40);
        const ringMat = new THREE.MeshStandardMaterial({
            color: 0x7be0ff,
            emissive: 0x256a84,
            emissiveIntensity: 0.7,
            metalness: 0.35,
            roughness: 0.35
        });
        const ring = new THREE.Mesh(ringGeo, ringMat);
        ring.rotation.x = Math.PI / 2;
        ring.position.copy(center);
        ring.position.y += 1;
        ring.userData = { type: 'marker', variant: 'center-ring', index: -2 };
        this.scene.add(ring);
        this.meshes.push(ring);
    }

    normalizeObjectType(rawType) {
        const t = (rawType || '').toString().toLowerCase();
        if (t.includes('surface') || t.includes('floor')) return 'platform';
        if (t.includes('platform') || t.includes('ground')) return 'platform';
        if (t.includes('enemy') || t.includes('npc') || t.includes('monster')) return 'enemy';
        if (t.includes('collect') || t.includes('coin') || t.includes('bonus') || t.includes('pickup')) return 'collectible';
        if (t.includes('shadow')) return 'obstacle';
        if (t.includes('obstacle') || t.includes('wall') || t.includes('block')) return 'obstacle';
        return 'obstacle';
    }

    createGeometryByType(type, width, height, depth, index) {
        if (type === 'platform') {
            return new THREE.BoxGeometry(width, height, depth, 2, 1, 2);
        }

        if (type === 'enemy') {
            if (index % 3 === 0) return new THREE.CylinderGeometry(width * 0.45, width * 0.55, height * 1.2, 18);
            if (index % 3 === 1) return new THREE.ConeGeometry(width * 0.55, height * 1.25, 18);
            return new THREE.OctahedronGeometry(Math.max(width, depth) * 0.42, 1);
        }

        if (type === 'collectible') {
            if (index % 3 === 0) return new THREE.SphereGeometry(width * 0.35, 18, 14);
            if (index % 3 === 1) return new THREE.TorusGeometry(width * 0.33, width * 0.12, 14, 22);
            return new THREE.IcosahedronGeometry(width * 0.34, 1);
        }

        if (index % 2 === 0) return new THREE.BoxGeometry(width, height, depth, 2, 2, 2);
        return new THREE.TetrahedronGeometry(Math.max(width, depth) * 0.5, 1);
    }

    addGroundPlane() {
        const groundGeometry = new THREE.PlaneGeometry(3000, 3000, 20, 20);
        const groundMaterial = new THREE.MeshStandardMaterial({
            color: 0x1a2026,
            roughness: 0.92,
            metalness: 0.05
        });
        const ground = new THREE.Mesh(groundGeometry, groundMaterial);
        ground.rotation.x = -Math.PI / 2;
        ground.position.y = -55;
        ground.receiveShadow = true;
        ground.userData.type = 'ground';
        this.scene.add(ground);
    }

    setSourceImage(dataUrl) {
        if (!dataUrl) return;
        if (this.sourceImageUrl === dataUrl && this.sourceImageTexture) return;

        this.sourceImageUrl = dataUrl;
        this.sourceImageData = null;
        this.sourceImageWidth = 0;
        this.sourceImageHeight = 0;
        this.sourceEdgeMap = null;
        this.sourceImageTexture = null;
        this.sourceTextureFailed = false;

        const img = new Image();
        img.onload = () => {
            const canvas = document.createElement('canvas');
            canvas.width = img.width;
            canvas.height = img.height;
            const ctx = canvas.getContext('2d');
            ctx.drawImage(img, 0, 0);
            const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            this.sourceImageData = imageData.data;
            this.sourceImageWidth = canvas.width;
            this.sourceImageHeight = canvas.height;
            this.sourceEdgeMap = this.buildEdgeMapFromSource();
            console.log('✓ Source image pixels loaded');
            this.maybeRunDeferredAnalysis();
        };
        img.src = dataUrl;

        const loader = new THREE.TextureLoader();
        loader.load(
            dataUrl,
            (texture) => {
                texture.wrapS = THREE.ClampToEdgeWrapping;
                texture.wrapT = THREE.ClampToEdgeWrapping;
                texture.encoding = THREE.sRGBEncoding;
                this.sourceImageTexture = texture;
                console.log('✓ Source image texture loaded');
                // If the terrain was already built before the texture arrived, drape it now.
                const terrain = this.meshes.find(m => m.userData && m.userData.variant === 'photo-relief');
                if (terrain && !terrain.material.map) {
                    terrain.material.map = this.makeDrapedTexture();
                    terrain.material.color.setHex(0xffffff);
                    terrain.material.needsUpdate = true;
                }
                this.maybeRunDeferredAnalysis();
            },
            undefined,
            (err) => {
                console.warn('⚠️ Failed to load source image texture', err);
                this.sourceTextureFailed = true;
                this.maybeRunDeferredAnalysis();
            }
        );
    }

    // Run a deferred scene build only once BOTH the photo pixels (for height/colour sampling)
    // and the photo texture (for draping) are ready — otherwise the relief renders untextured.
    maybeRunDeferredAnalysis() {
        if (!this.deferredAnalysisData) return;
        const textureReady = this.sourceImageTexture || this.sourceTextureFailed;
        if (this.sourceImageData && textureReady) {
            const pending = this.deferredAnalysisData;
            this.deferredAnalysisData = null;
            this.loadAnalysisDataInternal(pending);
        }
    }

    sampleSourceColor(u, v) {
        const rgb = this.sampleSourceRgb(u, v);
        if (!rgb) return null;
        return new THREE.Color(rgb.r, rgb.g, rgb.b).getHex();
    }

    sampleSourceRgb(u, v) {
        if (!this.sourceImageData || !this.sourceImageWidth || !this.sourceImageHeight) return null;
        const clampedU = Math.max(0, Math.min(1, u));
        const clampedV = Math.max(0, Math.min(1, v));
        const x = Math.min(this.sourceImageWidth - 1, Math.floor(clampedU * (this.sourceImageWidth - 1)));
        const y = Math.min(this.sourceImageHeight - 1, Math.floor(clampedV * (this.sourceImageHeight - 1)));
        const idx = (y * this.sourceImageWidth + x) * 4;
        return {
            r: this.sourceImageData[idx] / 255,
            g: this.sourceImageData[idx + 1] / 255,
            b: this.sourceImageData[idx + 2] / 255
        };
    }

    computeHeightFromSource(u, v, repeat = 1, detail = 1) {
        const tiledU = ((u * repeat) % 1 + 1) % 1;
        const tiledV = ((v * repeat) % 1 + 1) % 1;

        const rgb = this.sampleSourceRgbPatch(tiledU, tiledV);
        if (!rgb) return null;

        const r = rgb.r;
        const g = rgb.g;
        const b = rgb.b;
        const max = Math.max(r, g, b);
        const min = Math.min(r, g, b);
        const sat = max - min;
        const luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        const greenDom = g - Math.max(r, b);

        const edgeBoost = this.sampleEdgeStrength(tiledU, tiledV);
        const localSlope = this.sampleLocalSlope(tiledU, tiledV);
        const terrainClass = this.classifyTerrainFromRgb(r, g, b, luma, sat, greenDom);

        const classBase = {
            path: 0.08,
            soil: 0.18,
            grass: 0.42,
            shrub: 0.58,
            tree: 0.7,
            other: 0.32
        }[terrainClass] ?? 0.32;

        const classBoost = {
            path: -0.06,
            soil: 0.02,
            grass: 0.18,
            shrub: 0.26,
            tree: 0.32,
            other: 0.08
        }[terrainClass] ?? 0.08;

        const textureBoost = Math.max(0, sat - 0.2) * 0.2 + Math.max(0, greenDom) * 0.25;
        const base = classBase + luma * 0.35;
        // edge/slope are high-frequency terms — scaling them down (detail<1) avoids spiky noise
        // so the terrain reads as smooth natural relief rather than a bed of nails.
        const height = base + classBoost + textureBoost + (edgeBoost * 0.75 + localSlope * 0.6) * detail;
        return Math.max(0, Math.min(1.6, height));
    }

    classifyTerrainFromRgb(r, g, b, luma, sat, greenDom) {
        if (sat < 0.14 && luma > 0.45) return 'path';
        if (r > g && r > b && sat > 0.2) return 'soil';
        if (greenDom > 0.12 && luma < 0.35) return 'tree';
        if (greenDom > 0.08 && sat > 0.22 && luma < 0.55) return 'shrub';
        if (greenDom > 0.05 && sat > 0.18) return 'grass';
        return 'other';
    }

    sampleLocalSlope(u, v) {
        const d = 0.01;
        const c = this.sampleLumaPatch(u, v) ?? 0;
        const x1 = this.sampleLumaPatch(Math.min(1, u + d), v) ?? c;
        const x0 = this.sampleLumaPatch(Math.max(0, u - d), v) ?? c;
        const y1 = this.sampleLumaPatch(u, Math.min(1, v + d)) ?? c;
        const y0 = this.sampleLumaPatch(u, Math.max(0, v - d)) ?? c;
        const gx = Math.abs(x1 - x0);
        const gy = Math.abs(y1 - y0);
        return Math.min(1, (gx + gy) * 1.8);
    }

    sampleLumaPatch(u, v) {
        const rgb = this.sampleSourceRgbPatch(u, v);
        if (!rgb) return null;
        return 0.2126 * rgb.r + 0.7152 * rgb.g + 0.0722 * rgb.b;
    }

    sampleSourceRgbPatch(u, v) {
        if (!this.sourceImageData || !this.sourceImageWidth || !this.sourceImageHeight) return null;
        const radius = 2;
        let rSum = 0;
        let gSum = 0;
        let bSum = 0;
        let count = 0;

        const baseX = Math.min(this.sourceImageWidth - 1, Math.floor(u * (this.sourceImageWidth - 1)));
        const baseY = Math.min(this.sourceImageHeight - 1, Math.floor(v * (this.sourceImageHeight - 1)));

        for (let dy = -radius; dy <= radius; dy++) {
            for (let dx = -radius; dx <= radius; dx++) {
                const x = Math.max(0, Math.min(this.sourceImageWidth - 1, baseX + dx));
                const y = Math.max(0, Math.min(this.sourceImageHeight - 1, baseY + dy));
                const idx = (y * this.sourceImageWidth + x) * 4;
                rSum += this.sourceImageData[idx];
                gSum += this.sourceImageData[idx + 1];
                bSum += this.sourceImageData[idx + 2];
                count++;
            }
        }

        if (count <= 0) return null;
        return {
            r: (rSum / count) / 255,
            g: (gSum / count) / 255,
            b: (bSum / count) / 255
        };
    }

    buildEdgeMapFromSource() {
        if (!this.sourceImageData || !this.sourceImageWidth || !this.sourceImageHeight) return null;
        const w = this.sourceImageWidth;
        const h = this.sourceImageHeight;
        const edge = new Float32Array(w * h);
        const lumaAt = (x, y) => {
            const idx = (y * w + x) * 4;
            const r = this.sourceImageData[idx] / 255;
            const g = this.sourceImageData[idx + 1] / 255;
            const b = this.sourceImageData[idx + 2] / 255;
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        };

        for (let y = 1; y < h - 1; y++) {
            for (let x = 1; x < w - 1; x++) {
                const tl = lumaAt(x - 1, y - 1);
                const tc = lumaAt(x, y - 1);
                const tr = lumaAt(x + 1, y - 1);
                const ml = lumaAt(x - 1, y);
                const mr = lumaAt(x + 1, y);
                const bl = lumaAt(x - 1, y + 1);
                const bc = lumaAt(x, y + 1);
                const br = lumaAt(x + 1, y + 1);

                const gx = (tr + 2 * mr + br) - (tl + 2 * ml + bl);
                const gy = (bl + 2 * bc + br) - (tl + 2 * tc + tr);
                const mag = Math.min(1, Math.sqrt(gx * gx + gy * gy));
                edge[y * w + x] = mag;
            }
        }

        return edge;
    }

    sampleEdgeStrength(u, v) {
        if (!this.sourceEdgeMap || !this.sourceImageWidth || !this.sourceImageHeight) return 0;
        const x = Math.min(this.sourceImageWidth - 1, Math.floor(u * (this.sourceImageWidth - 1)));
        const y = Math.min(this.sourceImageHeight - 1, Math.floor(v * (this.sourceImageHeight - 1)));
        return this.sourceEdgeMap[y * this.sourceImageWidth + x] || 0;
    }

    clearDynamicMeshes() {
        this.meshes.forEach(mesh => this.scene.remove(mesh));
        this.meshes = [];
    }

    applyStyleTint(colorHex, style) {
        if (!style) return colorHex;
        const tintR = Number(style.tintR ?? style.TintR ?? 255);
        const tintG = Number(style.tintG ?? style.TintG ?? 255);
        const tintB = Number(style.tintB ?? style.TintB ?? 255);
        const base = new THREE.Color(colorHex);
        const r = Math.min(255, Math.round(base.r * tintR));
        const g = Math.min(255, Math.round(base.g * tintG));
        const b = Math.min(255, Math.round(base.b * tintB));
        return new THREE.Color(r / 255, g / 255, b / 255).getHex();
    }

    loadWorldState(worldState) {
        if (!worldState) return;

        console.log('🧠 Loading world state...');
        this.clearDynamicMeshes();
        this.addGroundPlane();

        const objects = worldState.objects || worldState.Objects || [];
        const terrain = worldState.terrainHeights || worldState.TerrainHeights || [];
        const gridSize = worldState.gridSize || worldState.GridSize || 0;
        const style = worldState.style || worldState.Style || {};

        this.addTerrainFromWorldState(terrain, gridSize, style);
        this.addObjectsFromWorldState(objects, style);
        this.applyWorldCamera(worldState.camera || worldState.Camera);

        // Always add visibility markers.
        this.addVisibilityMarkers(new THREE.Vector3(0, -40, 0));
    }

    addTerrainFromWorldState(terrain, gridSize, style) {
        if (!gridSize || !Array.isArray(terrain) || terrain.length === 0) return;

        const worldRange = 900;
        const segments = gridSize - 1;
        const maxHeight = 120;

        const geometry = new THREE.PlaneGeometry(worldRange, worldRange, segments, segments);
        const positions = geometry.attributes.position;

        for (let i = 0; i < positions.count; i++) {
            const x = positions.getX(i);
            const y = positions.getY(i);
            const u = (x / worldRange) + 0.5;
            const v = (y / worldRange) + 0.5;
            const gx = Math.max(0, Math.min(gridSize - 1, Math.round(u * (gridSize - 1))));
            const gz = Math.max(0, Math.min(gridSize - 1, Math.round(v * (gridSize - 1))));
            const idx = gz * gridSize + gx;
            const h = Math.max(0, Math.min(1.2, Number(terrain[idx] || 0)));
            positions.setZ(i, h * maxHeight);
        }

        geometry.rotateX(-Math.PI / 2);
        geometry.computeVertexNormals();

        const baseColor = this.applyStyleTint(0xffffff, style);
        const materialOptions = {
            color: baseColor,
            roughness: 0.85,
            metalness: 0.05,
            side: THREE.DoubleSide
        };
        if (this.sourceImageTexture) {
            materialOptions.map = this.sourceImageTexture;
        }

        const terrainMesh = new THREE.Mesh(
            geometry,
            new THREE.MeshStandardMaterial(materialOptions)
        );
        terrainMesh.position.set(0, -50, 0);
        terrainMesh.receiveShadow = true;
        terrainMesh.userData = { type: 'terrain', variant: 'world-heightmap', index: 0 };
        this.scene.add(terrainMesh);
        this.meshes.push(terrainMesh);
    }

    // A clone of the source photo prepared to be draped once over the terrain.
    // flipY=false makes texture row 0 (top of photo) align with the same (u,v) used for the
    // height sampling, so the image matches the relief instead of being upside-down/tiled.
    makeDrapedTexture() {
        if (!this.sourceImageTexture) return null;
        const t = this.sourceImageTexture.clone();
        t.wrapS = THREE.ClampToEdgeWrapping;
        t.wrapT = THREE.ClampToEdgeWrapping;
        t.repeat.set(1, 1);
        t.offset.set(0, 0);
        t.flipY = false;
        t.encoding = THREE.sRGBEncoding;
        if (this.renderer && this.renderer.capabilities) {
            t.anisotropy = this.renderer.capabilities.getMaxAnisotropy();
        }
        t.needsUpdate = true;
        return t;
    }

    addDepthHeightmap(depthGrid, style = {}) {
        if (!depthGrid) return false;

        const values = depthGrid.values || depthGrid.Values || depthGrid;
        const gridSize = depthGrid.size || depthGrid.Size || Math.round(Math.sqrt(values?.length || 0));
        if (!Array.isArray(values) || values.length === 0 || !gridSize || gridSize < 2) return false;

        // Drape the photo across the terrain exactly ONCE (no tiling) so the relief reads as the photo.
        const grid = this.buildExtendedGrid(values, gridSize, 1);
        const worldRange = 1400;
        const segments = 200; // relief detail comes from the full-res photo, not the coarse grid
        const baseY = -50;
        const maxHeight = 190;

        // Bilinear sample of the depth grid -> smooth height in 0..1 (coarse grid, interpolated).
        const sampleDepthGrid = (u, v) => {
            const n = grid.size;
            const gu = Math.max(0, Math.min(1, u)) * (n - 1);
            const gv = Math.max(0, Math.min(1, v)) * (n - 1);
            const ix = Math.floor(gu), iy = Math.floor(gv);
            const ix1 = Math.min(n - 1, ix + 1), iy1 = Math.min(n - 1, iy + 1);
            const fx = gu - ix, fy = gv - iy;
            const v00 = Number(grid.values[iy * n + ix] || 0);
            const v10 = Number(grid.values[iy * n + ix1] || 0);
            const v01 = Number(grid.values[iy1 * n + ix] || 0);
            const v11 = Number(grid.values[iy1 * n + ix1] || 0);
            return v00 * (1 - fx) * (1 - fy) + v10 * fx * (1 - fy)
                 + v01 * (1 - fx) * fy + v11 * fx * fy;
        };

        // Relief source:
        //   'depth' — РЕАЛЬНАЯ глубина из нейросети Depth Anything V2 (истинная 3D-форма сцены)
        //   'photo' — старая эвристика по яркости/цвету самого фото
        // По умолчанию: depth, если бэкенд вернул настоящую карту глубины модели; демо может форсировать режим.
        const reliefMode = this.forcedReliefMode || (this.depthIsReal ? 'depth' : 'photo');
        const sampleNormalizedHeight = (u, v) => {
            if (reliefMode === 'depth') {
                // 1 = ближе (передний план выше), 0 = дальше; мягкое усиление для читаемого рельефа.
                return Math.max(0, Math.min(1.6, sampleDepthGrid(u, v) * 1.45));
            }
            const fromSource = this.computeHeightFromSource(u, v, 1, 0.25);
            if (fromSource != null) return fromSource;
            return Math.max(0, Math.min(1.6, sampleDepthGrid(u, v)));
        };

        const gridN = segments + 1;
        const geometry = new THREE.PlaneGeometry(worldRange, worldRange, segments, segments);
        const positions = geometry.attributes.position;

        // 1) sample a normalized height for every grid vertex
        let heights = new Float32Array(positions.count);
        for (let i = 0; i < positions.count; i++) {
            const u = (positions.getX(i) / worldRange) + 0.5;
            const v = (positions.getY(i) / worldRange) + 0.5;
            heights[i] = sampleNormalizedHeight(u, v);
        }

        // 2) smooth the heightfield (3x3 average, several passes) -> rolling hills, not a bed of nails
        const at = (ix, iy) => iy * gridN + ix;
        for (let pass = 0; pass < 4; pass++) {
            const next = new Float32Array(heights.length);
            for (let iy = 0; iy < gridN; iy++) {
                for (let ix = 0; ix < gridN; ix++) {
                    let sum = 0, n = 0;
                    for (let dy = -1; dy <= 1; dy++) {
                        for (let dx = -1; dx <= 1; dx++) {
                            const nx = ix + dx, ny = iy + dy;
                            if (nx >= 0 && nx < gridN && ny >= 0 && ny < gridN) { sum += heights[at(nx, ny)]; n++; }
                        }
                    }
                    next[at(ix, iy)] = sum / n;
                }
            }
            heights = next;
        }

        // 3) write smoothed heights into the geometry
        for (let i = 0; i < positions.count; i++) {
            positions.setZ(i, heights[i] * maxHeight);
        }

        geometry.rotateX(-Math.PI / 2);
        geometry.computeVertexNormals();

        const baseColor = this.applyStyleTint(0xffffff, style);
        const materialOptions = {
            color: baseColor,
            roughness: 0.95,
            metalness: 0.0
        };
        const drapedTexture = this.makeDrapedTexture();
        if (drapedTexture) {
            materialOptions.map = drapedTexture;
        }

        const terrainMesh = new THREE.Mesh(
            geometry,
            new THREE.MeshStandardMaterial(materialOptions)
        );
        terrainMesh.position.set(0, baseY, 0);
        terrainMesh.castShadow = true;
        terrainMesh.receiveShadow = true;
        terrainMesh.userData = { type: 'terrain', variant: 'photo-relief', index: 0 };
        this.scene.add(terrainMesh);
        this.meshes.push(terrainMesh);

        // Solid base block under the relief so the terrain looks like a chunk of ground, not floating paper.
        const baseSlab = new THREE.Mesh(
            new THREE.BoxGeometry(worldRange, 120, worldRange),
            new THREE.MeshStandardMaterial({ color: 0x5b5147, roughness: 1.0, metalness: 0.0 })
        );
        baseSlab.position.set(0, baseY - 60, 0);
        baseSlab.receiveShadow = true;
        baseSlab.castShadow = true;
        baseSlab.userData = { type: 'terrain', variant: 'base-slab', index: -1 };
        this.scene.add(baseSlab);
        this.meshes.push(baseSlab);

        // Expose the SMOOTHED surface sampler (bilinear) so level objects rest exactly on the relief.
        this.terrainInfo = {
            worldRange,
            baseY,
            maxHeight,
            heightAt: (u, v) => {
                const gu = Math.max(0, Math.min(1, u)) * (gridN - 1);
                const gv = Math.max(0, Math.min(1, 1 - v)) * (gridN - 1);
                const ix = Math.floor(gu), iy = Math.floor(gv);
                const ix1 = Math.min(gridN - 1, ix + 1), iy1 = Math.min(gridN - 1, iy + 1);
                const fx = gu - ix, fy = gv - iy;
                const h = heights[at(ix, iy)] * (1 - fx) * (1 - fy)
                        + heights[at(ix1, iy)] * fx * (1 - fy)
                        + heights[at(ix, iy1)] * (1 - fx) * fy
                        + heights[at(ix1, iy1)] * fx * fy;
                return baseY + h * maxHeight;
            }
        };

        return true;
    }

    buildExtendedGrid(values, size, extendFactor = 1) {
        const baseSize = size;
        const extendedSize = Math.max(2, Math.floor(baseSize * extendFactor));
        const extendedValues = new Array(extendedSize * extendedSize).fill(0);
        const toIndex = (gx, gz, s) => gz * s + gx;

        const sampleBase = (gx, gz) => {
            const x = ((gx % baseSize) + baseSize) % baseSize;
            const z = ((gz % baseSize) + baseSize) % baseSize;
            return values[toIndex(x, z, baseSize)] || 0;
        };

        for (let gz = 0; gz < extendedSize; gz++) {
            for (let gx = 0; gx < extendedSize; gx++) {
                const base = sampleBase(gx, gz);
                extendedValues[toIndex(gx, gz, extendedSize)] = Math.max(0, Math.min(1.8, base));
            }
        }

        return { size: extendedSize, values: extendedValues };
    }

    addProceduralDecor(extendedGrid, worldRange) {
        if (!extendedGrid || !extendedGrid.values || extendedGrid.values.length === 0) return;

        const size = extendedGrid.size;
        const values = extendedGrid.values;
        const decorCount = Math.min(320, Math.max(160, Math.floor(size * size * 0.015)));
        const treeGeo = new THREE.ConeGeometry(14, 38, 7);
        const treeMat = new THREE.MeshStandardMaterial({ color: 0x4b7d4a, roughness: 0.85 });
        const rockGeo = new THREE.IcosahedronGeometry(9, 0);
        const rockMat = new THREE.MeshStandardMaterial({ color: 0x7d7f86, roughness: 0.9 });

        for (let i = 0; i < decorCount; i++) {
            const gx = Math.floor(Math.random() * size);
            const gz = Math.floor(Math.random() * size);
            const h = values[gz * size + gx] || 0;
            if (h < 0.08) continue;

            const x = (gx / (size - 1) - 0.5) * worldRange;
            const z = (gz / (size - 1) - 0.5) * worldRange;
            const y = -50 + h * 180;

            const isTree = Math.random() > 0.35;
            const mesh = new THREE.Mesh(isTree ? treeGeo : rockGeo, isTree ? treeMat : rockMat);
            const scale = isTree ? 0.7 + Math.random() * 0.9 : 0.5 + Math.random() * 0.7;
            mesh.position.set(x, y + (isTree ? 16 : 4), z);
            mesh.scale.set(scale, scale, scale);
            mesh.rotation.y = Math.random() * Math.PI * 2;
            mesh.castShadow = true;
            mesh.receiveShadow = true;
            mesh.userData = { type: 'decor', variant: isTree ? 'tree' : 'rock', index: i };
            this.scene.add(mesh);
            this.meshes.push(mesh);
        }
    }

    addObjectsFromWorldState(objects, style) {
        if (!Array.isArray(objects)) return;
        const worldRange = 900;

        objects.forEach((obj, index) => {
            const type = this.normalizeObjectType(obj.type || obj.Type || 'obstacle');
            const width = Math.max(18, (obj.width ?? obj.Width ?? 0.06) * worldRange * 0.7);
            const depth = Math.max(18, (obj.height ?? obj.Height ?? 0.06) * worldRange * 0.7);
            const height = Math.max(20, (obj.depth ?? obj.Depth ?? 0.08) * worldRange);

            const rawColor = this.colorFromObject(obj, type);
            const color = this.applyStyleTint(rawColor, style);
            const geometry = this.createGeometryByType(type, width, height, depth, index);
            const usePhotoMaterial = this.sourceImageTexture && (type === 'platform' || type === 'obstacle');
            const material = usePhotoMaterial
                ? this.createPhotoMaterial(type, color)
                : this.createMaterial(type, color);
            const mesh = new THREE.Mesh(geometry, material);

            const x = ((obj.x ?? obj.X ?? 0.5) - 0.5) * worldRange;
            const z = ((obj.y ?? obj.Y ?? 0.5) - 0.5) * worldRange;
            const y = -45 + height * 0.5 + (type === 'collectible' ? 12 : 0);
            mesh.position.set(x, y, z);
            mesh.castShadow = true;
            mesh.receiveShadow = true;
            mesh.userData = { type, variant: obj.type || obj.Type || type, index };

            this.scene.add(mesh);
            this.meshes.push(mesh);
        });
    }

    applyWorldCamera(cameraState) {
        if (!cameraState) return;
        const x = Number(cameraState.x ?? cameraState.X ?? 0);
        const y = Number(cameraState.y ?? cameraState.Y ?? 240);
        const z = Number(cameraState.z ?? cameraState.Z ?? 360);
        const yaw = Number(cameraState.yaw ?? cameraState.Yaw ?? 0);
        const pitch = Number(cameraState.pitch ?? cameraState.Pitch ?? -0.25);

        this.camera.position.set(x, y, z);
        const look = new THREE.Vector3(
            x + Math.sin(yaw) * 200,
            y + Math.sin(pitch) * 120,
            z + Math.cos(yaw) * 200
        );
        this.camera.lookAt(look);
        this.controls.target.copy(look);
        this.controls.update();
        this.renderer.render(this.scene, this.camera);
    }

    loadAnalysisData(analysisData) {
        this.analysisData = analysisData;

        const textureReady = this.sourceImageTexture || this.sourceTextureFailed;
        if (this.sourceImageUrl && (!this.sourceImageData || !textureReady)) {
            this.deferredAnalysisData = analysisData;
            this.waitForSourceImagePixels(0);
            return;
        }

        this.loadAnalysisDataInternal(analysisData);
    }

    waitForSourceImagePixels(attempt) {
        const textureReady = this.sourceImageTexture || this.sourceTextureFailed;
        if (this.sourceImageData && textureReady) {
            if (this.deferredAnalysisData) {
                const pending = this.deferredAnalysisData;
                this.deferredAnalysisData = null;
                this.loadAnalysisDataInternal(pending);
            }
            return;
        }

        if (attempt >= 30) {
            console.warn('⚠️ Source image not fully ready, proceeding (texture will be draped when it arrives)');
            if (this.deferredAnalysisData) {
                const pending = this.deferredAnalysisData;
                this.deferredAnalysisData = null;
                this.loadAnalysisDataInternal(pending);
            }
            return;
        }

        setTimeout(() => this.waitForSourceImagePixels(attempt + 1), 100);
    }

    // Демо/диагностика: форсировать источник рельефа ('depth' | 'photo' | null=авто) и перестроить сцену.
    setReliefMode(mode) {
        this.forcedReliefMode = mode;
        if (this.analysisData) this.loadAnalysisDataInternal(this.analysisData);
    }

    loadAnalysisDataInternal(analysisData) {
        this.analysisData = analysisData;

        // Pick up the current container size (the card may have just become visible).
        if (this.container && this.container.clientWidth > 0) {
            this.onWindowResize();
        }

        console.log('📊 Loading detailed analysis data...');
        console.log('Analysis:', analysisData);
        
        // Очистить старые меши
        this.meshes.forEach(mesh => this.scene.remove(mesh));
        this.meshes = [];

        // Получить параметры из анализа
        const detailAnalysis = analysisData.analysis?.detailed_analysis || {};
        const detailedObjects = analysisData.analysis?.detailed_objects || [];

        console.log(`🎯 Loading 3D scene with high-detail geometry`);
        console.log(`   - Detailed Objects: ${Array.isArray(detailedObjects) ? detailedObjects.length : 0}`);

        const depthGrid = analysisData.analysis?.depth_grid;
        // Глубина считается «настоящей», если бэкенд получил её от нейросети Depth Anything V2.
        this.depthIsReal = (analysisData.analysis_source === 'depth-anything-v2');
        this.terrainInfo = null;
        const hasDepth = this.addDepthHeightmap(depthGrid, {});

        if (hasDepth) {
            // Рельеф из фото — это сам уровень. Объекты расставляются прямо на его поверхности,
            // совпадая с тем, где они были обнаружены на снимке.
            this.setHelpersVisible(false);
            this.placeObjectsOnTerrain(analysisData, detailedObjects);
        } else {
            // Fallback (нет карты глубины): прежняя структурная реконструкция.
            this.addGroundPlane();
            this.generateDetailedLevel(analysisData, detailAnalysis, detailedObjects, { useStructureGrid: true });
        }

        // Сфокусировать камеру на центр объектов
        this.fitCameraToObjects();
    }

    // Rest detected level objects on top of the photo relief. Positions come from the object's
    // location in the photo (u,v), the surface height from the terrain sampler, and the colour
    // from the photo itself — so objects line up with what they were detected from.
    placeObjectsOnTerrain(analysisData, detailedObjects) {
        if (!this.terrainInfo) return;
        const objects = Array.isArray(detailedObjects) ? detailedObjects : [];
        if (objects.length === 0) return;

        const { worldRange } = this.terrainInfo;
        const imgW = Math.max(1, this.getObjectNumber(analysisData.level_2d, 'width', 'Width', 0)
            || Math.max(...objects.map(o => this.getObjectNumber(o, 'x', 'X', 0) + this.getObjectNumber(o, 'width', 'Width', 0)), 1));
        const imgH = Math.max(1, this.getObjectNumber(analysisData.level_2d, 'height', 'Height', 0)
            || Math.max(...objects.map(o => this.getObjectNumber(o, 'y', 'Y', 0) + this.getObjectNumber(o, 'height', 'Height', 0)), 1));

        const placeable = new Set(['platform', 'obstacle', 'collectible']);
        const pxToWorld = worldRange / Math.max(imgW, imgH);
        const minArea = (imgW * imgH) * 0.0008;

        const prepared = objects
            .map((obj, i) => ({ obj, i, area: this.getObjectNumber(obj, 'width', 'Width', 0) * this.getObjectNumber(obj, 'height', 'Height', 0) }))
            .filter(({ obj, area }) => placeable.has(this.normalizeObjectType(this.getObjectString(obj, 'type', 'Type', ''))) && area >= minArea)
            .sort((a, b) => b.area - a.area)
            .slice(0, 60);

        prepared.forEach(({ obj, i }) => {
            const type = this.normalizeObjectType(this.getObjectString(obj, 'type', 'Type', 'platform'));
            const px = this.getObjectNumber(obj, 'x', 'X', 0);
            const py = this.getObjectNumber(obj, 'y', 'Y', 0);
            const pw = this.getObjectNumber(obj, 'width', 'Width', 20);
            const ph = this.getObjectNumber(obj, 'height', 'Height', 20);

            const u = (px + pw / 2) / imgW;
            const v = (py + ph / 2) / imgH;
            const x = (u - 0.5) * worldRange;
            // terrain world-Z for image-row v is -(v-0.5)*range (plane is rotated -90° about X)
            const z = -(v - 0.5) * worldRange;
            const surfaceY = this.terrainInfo.heightAt(u, v);

            const w = Math.max(14, Math.min(180, pw * pxToWorld));
            const d = Math.max(14, Math.min(180, ph * pxToWorld));
            const color = this.sampleSourceColor(u, v) ?? this.colorFromObject(obj, type);

            let mesh;
            if (type === 'collectible') {
                const r = Math.max(7, Math.min(16, Math.min(w, d) * 0.4));
                mesh = new THREE.Mesh(
                    new THREE.IcosahedronGeometry(r, 0),
                    new THREE.MeshStandardMaterial({ color: 0xffd54a, emissive: 0x6e5300, roughness: 0.3, metalness: 0.7 })
                );
                mesh.position.set(x, surfaceY + r + 8, z);
            } else if (type === 'obstacle') {
                const r = Math.max(10, Math.min(60, Math.max(w, d) * 0.5));
                mesh = new THREE.Mesh(
                    new THREE.DodecahedronGeometry(r, 0),
                    new THREE.MeshStandardMaterial({ color, roughness: 0.95, metalness: 0.05, flatShading: true })
                );
                mesh.rotation.set(i * 0.7, i * 1.3, i * 0.4);
                mesh.position.set(x, surfaceY + r * 0.6, z);
            } else { // platform — a low slab resting on the relief
                const h = 12;
                mesh = new THREE.Mesh(
                    new THREE.BoxGeometry(w, h, d),
                    new THREE.MeshStandardMaterial({ color, roughness: 0.8, metalness: 0.1 })
                );
                mesh.position.set(x, surfaceY + h / 2, z);
            }

            mesh.castShadow = true;
            mesh.receiveShadow = true;
            mesh.userData = {
                type,
                variant: `${this.getObjectString(obj, 'type', 'Type', type)}-on-relief`,
                index: i,
                dimensions: { width: w, height: mesh.geometry.parameters?.height || w, depth: d }
            };
            this.scene.add(mesh);
            this.meshes.push(mesh);
        });

        console.log(`✓ Placed ${prepared.length} level objects on the photo relief`);
    }

    generateDetailedLevel(analysisData, detailAnalysis, detailedObjects, options = {}) {
        const stats = analysisData.geometry_stats || {};
        const fallbackCount = Math.max(12, stats.detected_platforms || 5);
        let rawObjects = Array.isArray(detailedObjects) && detailedObjects.length > 0
            ? detailedObjects
            : Array.from({ length: fallbackCount }, (_, i) => ({
                id: i,
                x: (i % 6) * 120,
                y: Math.floor(i / 6) * 90,
                width: 70 + (i % 4) * 20,
                height: 40 + (i % 3) * 18,
                type: i % 4 === 0 ? 'platform' : i % 4 === 1 ? 'enemy' : i % 4 === 2 ? 'collectible' : 'obstacle',
                color_r: 120 + (i * 11) % 120,
                color_g: 90 + (i * 17) % 130,
                color_b: 70 + (i * 23) % 140
            }));

        if (!Array.isArray(rawObjects) || rawObjects.length === 0) {
            rawObjects = [{
                id: 0,
                x: 0,
                y: 0,
                width: 120,
                height: 90,
                type: 'platform',
                color_r: 160,
                color_g: 180,
                color_b: 200
            }];
        }

        const minX = Math.min(...rawObjects.map(o => this.getObjectNumber(o, 'x', 'X', 0)));
        const maxX = Math.max(...rawObjects.map(o => this.getObjectNumber(o, 'x', 'X', 0)));
        const minY = Math.min(...rawObjects.map(o => this.getObjectNumber(o, 'y', 'Y', 0)));
        const maxY = Math.max(...rawObjects.map(o => this.getObjectNumber(o, 'y', 'Y', 0)));
        const spanX = Math.max(1, maxX - minX);
        const spanY = Math.max(1, maxY - minY);
        const sceneArea = spanX * spanY;
        const minArea = Math.max(24, sceneArea * 0.0006);
        const maxCount = 220;

        const objects = rawObjects
            .map(obj => ({
                ...obj,
                __area: Math.max(0, this.getObjectNumber(obj, 'width', 'Width', 0) * this.getObjectNumber(obj, 'height', 'Height', 0))
            }))
            .filter(obj => obj.__area >= minArea)
            .sort((a, b) => b.__area - a.__area)
            .slice(0, maxCount);

        if (objects.length === 0) {
            objects.push(...rawObjects.slice(0, Math.min(rawObjects.length, 24)));
        }
        const rawScale = 700 / Math.max(spanX, spanY);
        const worldScale = Math.min(3.0, Math.max(0.75, rawScale));
        const worldRange = 900;

        console.log(`🏗️ Generating ${objects.length} detailed meshes, scale=${worldScale.toFixed(2)}`);

        objects.forEach((obj, index) => {
            let type = this.normalizeObjectType(this.getObjectString(obj, 'type', 'Type', 'obstacle'));
            const width = Math.min(220, Math.max(22, this.getObjectNumber(obj, 'width', 'Width', 60) * worldScale * 0.7));
            const depth = Math.min(220, Math.max(22, this.getObjectNumber(obj, 'height', 'Height', 45) * worldScale * 0.7));
            const baseHeight = type === 'platform' ? 18 : type === 'collectible' ? 24 : 34;
            const brightness = this.getObjectBrightness(obj);
            const brightnessFactor = 0.7 + (brightness / 255) * 0.9;
            const height = Math.max(baseHeight, Math.sqrt(width * depth) * 0.28 * brightnessFactor);
            const areaRatio = (width * depth) / (worldRange * worldRange);
            if ((type === 'enemy' || type === 'collectible') && areaRatio > 0.08) {
                type = 'platform';
            }
            if (type === 'obstacle' && areaRatio > 0.2) {
                type = 'platform';
            }
            const rawX = this.getObjectNumber(obj, 'x', 'X', 0);
            const rawY = this.getObjectNumber(obj, 'y', 'Y', 0);
            const u = (rawX - minX) / spanX;
            const v = (rawY - minY) / spanY;
            const sampled = this.sampleSourceColor(u, v);
            const color = sampled ?? this.colorFromObject(obj, type);

            const usePhotoMaterial = this.sourceImageTexture && (type === 'platform' || type === 'obstacle');
            const useSolidColor = this.sourceImageTexture;
            const geometry = this.createGeometryByType(type, width, height, depth, index);
            const material = usePhotoMaterial
                ? this.createPhotoMaterial(type, color)
                : (useSolidColor ? this.createSolidMaterial(type, color) : this.createMaterial(type, color));
            const mesh = new THREE.Mesh(geometry, material);

            const nx = ((rawX - minX) / spanX) - 0.5;
            const nz = ((rawY - minY) / spanY) - 0.5;
            const x = nx * worldRange;
            const z = nz * worldRange;
            const y = -45 + height * 0.5 + (type === 'collectible' ? 12 : 0);

            // Structural footprint layer: keeps geometry layout close to photo-detected rectangles.
            const footprintHeight = Math.max(4, Math.min(12, height * 0.22));
            const footprintGeo = new THREE.BoxGeometry(width, footprintHeight, depth, 1, 1, 1);
            const footprintMat = usePhotoMaterial
                ? this.createPhotoMaterial(type, color)
                : (useSolidColor ? this.createSolidMaterial(type, color) : this.createMaterial(type, color));
            const footprint = new THREE.Mesh(footprintGeo, footprintMat);
            footprint.position.set(x, -50 + footprintHeight * 0.5, z);
            footprint.castShadow = true;
            footprint.receiveShadow = true;
            footprint.userData = {
                type,
                variant: `${this.getObjectString(obj, 'type', 'Type', type)}-footprint`,
                index,
                dimensions: { width, height: footprintHeight, depth }
            };
            this.scene.add(footprint);
            this.meshes.push(footprint);

            mesh.position.set(x, y, z);

            if (type === 'collectible') {
                mesh.rotation.y = index * 0.18;
                mesh.rotation.x = 0.3 + (index % 4) * 0.08;
            } else if (type === 'enemy') {
                mesh.rotation.y = (index % 6) * 0.5;
            }

            mesh.castShadow = true;
            mesh.receiveShadow = true;
            mesh.userData.type = type;
            mesh.userData.variant = this.getObjectString(obj, 'type', 'Type', type);
            mesh.userData.index = index;
            mesh.userData.dimensions = { width, height, depth };

            this.scene.add(mesh);
            this.meshes.push(mesh);

            if (type === 'platform' && width > 40 && depth > 40) {
                const topGeo = new THREE.BoxGeometry(width * 0.82, Math.max(4, height * 0.22), depth * 0.82);
                const topMat = usePhotoMaterial
                    ? this.createPhotoMaterial('platform', color)
                    : (useSolidColor ? this.createSolidMaterial('platform', color) : this.createMaterial('platform', color));
                const topMesh = new THREE.Mesh(topGeo, topMat);
                topMesh.position.set(x, y + height * 0.62, z);
                topMesh.castShadow = true;
                topMesh.receiveShadow = true;
                topMesh.userData = { ...mesh.userData, variant: `${mesh.userData.variant}-top` };
                this.scene.add(topMesh);
                this.meshes.push(topMesh);
            }
        });

        // Structure reconstruction layer: converts object cloud into continuous terrain + boundary walls.
        const useStructureGrid = options.useStructureGrid !== false;
        if (useStructureGrid) {
            const structureGrid = this.createStructureGrid(objects, minX, spanX, minY, spanY, 26);
            this.addStructuredTerrain(structureGrid, worldRange);
        }

        // Always place a bright center marker so the user has a guaranteed visual anchor.
        this.addVisibilityMarkers(new THREE.Vector3(0, -40, 0));

        // Unlit beacon is visible even if light setup is wrong.
        const beacon = new THREE.Mesh(
            new THREE.BoxGeometry(44, 44, 44),
            new THREE.MeshBasicMaterial({ color: 0xffff00 })
        );
        beacon.position.set(0, 15, 0);
        beacon.userData = { type: 'marker', variant: 'unlit-beacon', index: -3 };
        this.scene.add(beacon);
        this.meshes.push(beacon);

        if (this.meshes.length === 0) {
            console.warn('⚠️ No objects generated, creating fallback platform');
            const fallback = new THREE.Mesh(
                new THREE.BoxGeometry(160, 20, 100),
                this.createMaterial('platform', 0x4a90e2)
            );
            fallback.position.set(0, -30, 0);
            fallback.castShadow = true;
            fallback.receiveShadow = true;
            fallback.userData = { type: 'platform', variant: 'fallback', index: 0 };
            this.scene.add(fallback);
            this.meshes.push(fallback);

            const fallbackFloor = new THREE.Mesh(
                new THREE.PlaneGeometry(600, 600, 1, 1),
                new THREE.MeshStandardMaterial({ color: 0x9fb7d4, roughness: 0.9 })
            );
            fallbackFloor.rotation.x = -Math.PI / 2;
            fallbackFloor.position.set(0, -50, 0);
            fallbackFloor.receiveShadow = true;
            fallbackFloor.userData = { type: 'ground', variant: 'fallback-floor', index: 1 };
            this.scene.add(fallbackFloor);
            this.meshes.push(fallbackFloor);
        }

        console.log(`✓ Generated ${this.meshes.length} 3D objects with high detail`);
        console.log(`📐 Placement bounds: spanX=${spanX.toFixed(2)}, spanY=${spanY.toFixed(2)}, worldRange=${worldRange}`);
    }

    fitCameraToObjects() {
        if (this.meshes.length === 0) return;

        const box = new THREE.Box3();
        const focusMeshes = this.meshes.filter(mesh => {
            const t = mesh.userData?.type;
            return t !== 'overlay' && t !== 'marker';
        });
        const meshesToFit = focusMeshes.length > 0 ? focusMeshes : this.meshes;
        meshesToFit.forEach(mesh => box.expandByObject(mesh));

        const size = box.getSize(new THREE.Vector3());
        const maxDim = Math.max(size.x, size.y, size.z);

        if (!Number.isFinite(maxDim) || maxDim <= 0) {
            console.warn('⚠️ Invalid bounding box for camera fit, applying fallback camera pose');
            this.camera.position.set(260, 220, 320);
            this.controls.target.set(0, -30, 0);
            this.camera.lookAt(this.controls.target);
            this.controls.update();
            this.renderer.render(this.scene, this.camera);
            return;
        }

        const fov = this.camera.fov * (Math.PI / 180);
        let cameraZ = Math.abs(maxDim / 2 / Math.tan(fov / 2));

        cameraZ *= 1.7;

        const center = box.getCenter(new THREE.Vector3());
        this.camera.near = 0.1;
        this.camera.far = Math.max(6000, cameraZ * 12);
        this.camera.updateProjectionMatrix();

        this.camera.position.set(
            center.x + cameraZ * 0.52,
            center.y + cameraZ * 0.55,
            center.z + cameraZ * 0.82
        );
        this.camera.lookAt(center);
        this.controls.target.copy(center);
        this.controls.maxDistance = cameraZ * 8;
        this.controls.minDistance = Math.max(40, cameraZ * 0.12);
        this.controls.update();
        this.renderer.render(this.scene, this.camera);
    }

    onMeshClick(event) {
        // Преобразовать координаты мыши в нормализованные устройства координаты
        const rect = this.renderer.domElement.getBoundingClientRect();
        this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

        this.raycaster.setFromCamera(this.mouse, this.camera);
        const intersects = this.raycaster.intersectObjects(this.meshes);

        // Деселект предыдущего меша
        if (this.selectedMesh && this.selectedMesh.userData.originalMaterial) {
            this.selectedMesh.material = this.selectedMesh.userData.originalMaterial;
            this.selectedMesh.userData.originalMaterial = null;
        }

        // Выбрать новый меш
        if (intersects.length > 0) {
            this.selectedMesh = intersects[0].object;
            this.selectedMesh.userData.originalMaterial = this.selectedMesh.material;
            this.selectedMesh.material = this.createMaterial('selected');
            this.showObjectDetails(this.selectedMesh);
        }
    }

    showObjectDetails(mesh) {
        const details = {
            type: mesh.userData.type || 'unknown',
            variant: mesh.userData.variant || 'Standard',
            index: mesh.userData.index || 0,
            position: mesh.position.clone(),
            scale: mesh.scale.clone(),
            rotation: mesh.rotation.clone(),
            dimensions: mesh.userData.dimensions || {},
            material: {
                color: mesh.material.color?.getHexString() || 'N/A',
                metalness: mesh.material.metalness?.toFixed(2) || 'N/A',
                roughness: mesh.material.roughness?.toFixed(2) || 'N/A',
                emissive: mesh.material.emissive?.getHexString() || 'N/A'
            }
        };

        // Отправить событие с деталями
        document.dispatchEvent(new CustomEvent('meshSelected', { detail: details }));
        
        // Показать панель с подробной информацией
        const objPanel = document.getElementById('objectPanel');
        if (objPanel) {
            document.getElementById('objType').textContent = details.type.toUpperCase();
            document.getElementById('objIndex').textContent = `${details.index} (${details.variant})`;
            document.getElementById('objPos').textContent = 
                `X: ${details.position.x.toFixed(1)}, Y: ${details.position.y.toFixed(1)}, Z: ${details.position.z.toFixed(1)}`;
            document.getElementById('objColor').textContent = details.material.color;
            objPanel.style.display = 'block';
            objPanel.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
        
        console.log('🎯 Selected:', details);
    }

    exportScene() {
        // Экспортировать сцену как JSON
        const exporter = new THREE.ObjectLoader();
        const sceneJSON = this.scene.toJSON(new THREE.ObjectLoaderCacheExporter());
        return {
            meshes: this.meshes.length,
            objects: this.meshes.map(m => ({
                type: m.userData.type,
                position: m.position.toArray(),
                scale: m.scale.toArray(),
                rotation: m.rotation.toArray()
            }))
        };
    }

    toggleAutoRotate() {
        if (!this.controls) {
            console.warn('⚠️ Controls not initialized');
            return;
        }
        this.controls.autoRotate = !this.controls.autoRotate;
        console.log(`🔄 Auto-rotate: ${this.controls.autoRotate}`);
    }

    resetCamera() {
        if (!this.controls) {
            console.warn('⚠️ Controls not initialized');
            return;
        }
        // Re-frame the current scene (terrain can span ~1400 units, so a fixed pose is too close).
        if (this.meshes.length > 0) {
            this.fitCameraToObjects();
        } else {
            this.camera.position.set(200, 150, 250);
            this.camera.lookAt(0, 0, 0);
            this.controls.target.set(0, 0, 0);
            this.controls.update();
        }
    }

    animate() {
        requestAnimationFrame(() => this.animate());
        
        // Safety checks
        if (!this.controls) {
            console.warn('⚠️ this.controls is null in animate()');
            return;
        }

        if (!this.renderer) {
            console.warn('⚠️ this.renderer is null in animate()');
            return;
        }

        this.controls.update();
        this.renderer.render(this.scene, this.camera);
    }

    dispose() {
        this.meshes.forEach(mesh => {
            if (mesh.geometry) mesh.geometry.dispose();
            if (mesh.material) mesh.material.dispose();
        });
        Object.values(this.textureCache || {}).forEach(texture => texture.dispose());
        if (this.renderer) this.renderer.dispose();
    }
    };

    console.log('✓ Viewer3D class successfully exported to window');
})(); // End IIFE

// Fallback: also attach to window if IIFE exports didn't work
if (typeof window.Viewer3D === 'undefined') {
    console.error('⚠️ Viewer3D not found on window after IIFE execution');
}
