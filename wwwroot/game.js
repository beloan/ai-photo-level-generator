/**
 * 2D Платформер игровой движок
 * Использует Canvas API для рендеринга
 */

class GameEngine {
    constructor(canvasId, levelData) {
        this.canvas = document.getElementById(canvasId);
        this.ctx = this.canvas.getContext('2d');
        this.levelData = levelData;
        
        console.log('🎮 GameEngine initialized with level:', {
            width: levelData.width,
            height: levelData.height,
            platforms: levelData.platforms?.length || 0,
            enemies: levelData.enemies?.length || 0,
            collectibles: levelData.collectibles?.length || 0,
            obstacles: levelData.obstacles?.length || 0,
            player_start: levelData.player_start,
            player_goal: levelData.player_goal
        });
        
        // Размеры
        this.width = this.canvas.width;
        this.height = this.canvas.height;
        
        // Масштаб: уровень 800x600 -> canvas
        this.scaleX = this.width / levelData.width;
        this.scaleY = this.height / levelData.height;
        
        console.log('📏 Scale factors:', { scaleX: this.scaleX, scaleY: this.scaleY });
        
        // Игрок
        this.player = {
            x: levelData.player_start.x,
            y: levelData.player_start.y,
            width: 30,
            height: 40,
            velocityX: 0,
            velocityY: 0,
            isJumping: false,
            health: 3,
            score: 0
        };
        
        // Физика
        this.gravity = 0.5;
        this.jumpPower = 12;
        this.moveSpeed = 4;
        
        // Игровое состояние
        this.gameOver = false;
        this.won = false;
        this.gameTime = 0;
        
        // Управление
        this.keys = {};
        this.setupControls();
        
        // Список собранных ресурсов
        this.collectedItems = [];
    }
    
    setupControls() {
        window.addEventListener('keydown', (e) => {
            this.keys[e.key] = true;
            
            // Прыжок
            if ((e.key === ' ' || e.key === 'w' || e.key === 'ArrowUp') && !this.player.isJumping) {
                this.player.velocityY = -this.jumpPower;
                this.player.isJumping = true;
            }
        });
        
        window.addEventListener('keyup', (e) => {
            this.keys[e.key] = false;
        });
    }
    
    update() {
        if (this.gameOver || this.won) return;
        
        this.gameTime++;
        
        // Движение игрока
        this.player.velocityX = 0;
        
        if (this.keys['a'] || this.keys['ArrowLeft']) {
            this.player.velocityX = -this.moveSpeed;
        }
        if (this.keys['d'] || this.keys['ArrowRight']) {
            this.player.velocityX = this.moveSpeed;
        }
        
        // Гравитация
        this.player.velocityY += this.gravity;
        
        // Обновить позицию
        this.player.x += this.player.velocityX;
        this.player.y += this.player.velocityY;
        
        // Границы экрана
        if (this.player.x < 0) this.player.x = 0;
        if (this.player.x + this.player.width > this.width) this.player.x = this.width - this.player.width;
        
        // Проверка падения
        if (this.player.y > this.height) {
            this.player.health--;
            if (this.player.health <= 0) {
                this.gameOver = true;
            } else {
                this.resetPlayerPosition();
            }
        }
        
        // Столкновения с платформами
        this.checkPlatformCollisions();
        
        // Столкновения с врагами
        this.checkEnemyCollisions();
        
        // Сбор ресурсов
        this.checkCollectibleCollisions();
        
        // Столкновения с препятствиями
        this.checkObstacleCollisions();
        
        // Проверка победы
        this.checkWinCondition();
        
        // Обновить врагов
        this.updateEnemies();
    }
    
    checkPlatformCollisions() {
        this.player.isJumping = true;
        
        for (let platform of this.levelData.platforms) {
            if (this.isColliding(
                this.player.x * this.scaleX, this.player.y * this.scaleY,
                this.player.width, this.player.height,
                platform.x * this.scaleX, platform.y * this.scaleY,
                platform.width * this.scaleX, platform.height * this.scaleY
            )) {
                // Если падаем сверху
                if (this.player.velocityY > 0) {
                    this.player.y = (platform.y - this.player.height) / this.scaleY;
                    this.player.velocityY = 0;
                    this.player.isJumping = false;
                }
            }
        }
    }
    
    checkEnemyCollisions() {
        for (let enemy of this.levelData.enemies) {
            if (this.isColliding(
                this.player.x * this.scaleX, this.player.y * this.scaleY,
                this.player.width, this.player.height,
                enemy.x * this.scaleX, enemy.y * this.scaleY,
                40, 40
            )) {
                this.player.health--;
                if (this.player.health <= 0) {
                    this.gameOver = true;
                } else {
                    this.resetPlayerPosition();
                }
            }
        }
    }
    
    checkCollectibleCollisions() {
        for (let i = this.levelData.collectibles.length - 1; i >= 0; i--) {
            let item = this.levelData.collectibles[i];
            
            if (this.isColliding(
                this.player.x * this.scaleX, this.player.y * this.scaleY,
                this.player.width, this.player.height,
                item.x * this.scaleX, item.y * this.scaleY,
                20, 20
            )) {
                this.player.score += item.value * 10;
                this.collectedItems.push(item);
                this.levelData.collectibles.splice(i, 1);
            }
        }
    }
    
    checkObstacleCollisions() {
        for (let obstacle of this.levelData.obstacles) {
            if (this.isColliding(
                this.player.x * this.scaleX, this.player.y * this.scaleY,
                this.player.width, this.player.height,
                obstacle.x * this.scaleX, obstacle.y * this.scaleY,
                obstacle.width * this.scaleX, obstacle.height * this.scaleY
            )) {
                this.player.health -= obstacle.damage;
                if (this.player.health <= 0) {
                    this.gameOver = true;
                } else {
                    this.resetPlayerPosition();
                }
            }
        }
    }
    
    checkWinCondition() {
        // Если дошёл до конца уровня
        const goal = this.levelData.player_goal || { x: 750, y: 500 };
        if (Math.abs(this.player.x - goal.x / this.levelData.width * this.width) < 50 &&
            Math.abs(this.player.y * this.scaleY - goal.y * this.scaleY) < 50) {
            this.won = true;
        }
    }
    
    updateEnemies() {
        // Простой ИИ врагов - ходят туда-сюда
        for (let enemy of this.levelData.enemies) {
            if (!enemy.direction) enemy.direction = 1;
            if (!enemy.x_original) {
                enemy.x_original = enemy.x;
                enemy.patrol_range = 100;
            }
            
            enemy.x += enemy.speed * enemy.direction * 0.5;
            
            if (Math.abs(enemy.x - enemy.x_original) > enemy.patrol_range) {
                enemy.direction *= -1;
            }
        }
    }
    
    resetPlayerPosition() {
        this.player.x = this.levelData.player_start.x / this.levelData.width * this.width;
        this.player.y = this.levelData.player_start.y / this.levelData.height * this.height;
        this.player.velocityX = 0;
        this.player.velocityY = 0;
    }
    
    isColliding(x1, y1, w1, h1, x2, y2, w2, h2) {
        return x1 < x2 + w2 &&
               x1 + w1 > x2 &&
               y1 < y2 + h2 &&
               y1 + h1 > y2;
    }
    
    render() {
            // Очистить экран
            this.ctx.fillStyle = '#1a1a2e';
            this.ctx.fillRect(0, 0, this.width, this.height);
            
            // Диагностика первого кадра
            if (this.gameTime === 1) {
                console.log('🎨 Rendering - Canvas background cleared');
            }
            
            // Рисовать платформы
            this.ctx.fillStyle = '#4a9eff';
            const platforms = this.levelData?.platforms || [];
            for (let platform of platforms) {
            this.ctx.fillRect(
                platform.x * this.scaleX,
                platform.y * this.scaleY,
                platform.width * this.scaleX,
                platform.height * this.scaleY
            );
            
            // Тип платформы
            this.ctx.fillStyle = '#ffffff';
            this.ctx.font = '10px Arial';
            if (platform.type === 'moving') {
                this.ctx.fillStyle = '#ffaa00';
            } else if (platform.type === 'fragile') {
                this.ctx.fillStyle = '#ff6b6b';
            }
        }
        
        // Рисовать врагов
        this.ctx.fillStyle = '#ff4444';
        const enemies = this.levelData?.enemies || [];
        for (let enemy of enemies) {
            this.ctx.beginPath();
            this.ctx.arc(
                enemy.x * this.scaleX,
                enemy.y * this.scaleY,
                10,
                0,
                Math.PI * 2
            );
            this.ctx.fill();
        }
        
        // Рисовать ресурсы
        const collectibles = this.levelData?.collectibles || [];
        for (let item of collectibles) {
            if (item.type === 'coin') {
                this.ctx.fillStyle = '#ffdd00';
            } else if (item.type === 'crystal') {
                this.ctx.fillStyle = '#00ffff';
            } else {
                this.ctx.fillStyle = '#ffff00';
            }
            this.ctx.beginPath();
            this.ctx.arc(
                item.x * this.scaleX,
                item.y * this.scaleY,
                8,
                0,
                Math.PI * 2
            );
            this.ctx.fill();
        }
        
        // Рисовать препятствия
        this.ctx.fillStyle = '#ff9900';
        for (let obstacle of this.levelData.obstacles) {
            this.ctx.fillRect(
                obstacle.x * this.scaleX,
                obstacle.y * this.scaleY,
                obstacle.width * this.scaleX,
                obstacle.height * this.scaleY
            );
        }
        
        // Рисовать точку выхода
        this.ctx.fillStyle = '#00ff00';
        this.ctx.fillRect(
            (this.levelData.player_goal?.x || 750) * this.scaleX - 20,
            (this.levelData.player_goal?.y || 500) * this.scaleY - 20,
            40,
            40
        );
        this.ctx.fillStyle = '#ffffff';
        this.ctx.font = 'bold 12px Arial';
        const goal = this.levelData.player_goal || { x: 750, y: 500 };
        this.ctx.fillText('EXIT', goal.x * this.scaleX - 15, goal.y * this.scaleY + 5);
        
        // Рисовать игрока
        this.ctx.fillStyle = '#ffaa00';
        this.ctx.fillRect(
            this.player.x * this.scaleX,
            this.player.y * this.scaleY,
            this.player.width,
            this.player.height
        );
        
        // Рисовать UI
        this.renderUI();
        
        // Game Over / Won экран
        if (this.gameOver) {
            this.ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
            this.ctx.fillRect(0, 0, this.width, this.height);
            this.ctx.fillStyle = '#ff0000';
            this.ctx.font = 'bold 48px Arial';
            this.ctx.textAlign = 'center';
            this.ctx.fillText('GAME OVER', this.width / 2, this.height / 2);
            this.ctx.font = '24px Arial';
            this.ctx.fillStyle = '#ffffff';
            this.ctx.fillText('Press R to restart', this.width / 2, this.height / 2 + 50);
        }
        
        if (this.won) {
            this.ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
            this.ctx.fillRect(0, 0, this.width, this.height);
            this.ctx.fillStyle = '#00ff00';
            this.ctx.font = 'bold 48px Arial';
            this.ctx.textAlign = 'center';
            this.ctx.fillText('YOU WIN!', this.width / 2, this.height / 2);
            this.ctx.font = '24px Arial';
            this.ctx.fillStyle = '#ffff00';
            this.ctx.fillText(`Score: ${this.player.score}`, this.width / 2, this.height / 2 + 50);
        }
        } catch (error) {
            console.error('❌ Render error:', error);
            console.error('🔍 Debug info:', {
                levelDataExists: !!this.levelData,
                platformsCount: this.levelData?.platforms?.length || 0,
                ctxExists: !!this.ctx,
                canvasExists: !!this.canvas
            });
        }
    }
    
    renderUI() {
        this.ctx.fillStyle = '#ffffff';
        this.ctx.font = 'bold 16px Arial';
        this.ctx.textAlign = 'left';
        
        // Health
        this.ctx.fillStyle = '#ff4444';
        this.ctx.fillText(`❤ Health: ${this.player.health}`, 10, 25);
        
        // Score
        this.ctx.fillStyle = '#ffff00';
        this.ctx.fillText(`⭐ Score: ${this.player.score}`, 10, 50);
        
        // Collected items
        this.ctx.fillStyle = '#00ff00';
        this.ctx.fillText(`💰 Collected: ${this.collectedItems.length}/${this.levelData.collectibles.length + this.collectedItems.length}`, 10, 75);
        
        // Controls
        this.ctx.fillStyle = '#aaaaaa';
        this.ctx.font = '12px Arial';
        this.ctx.fillText('A/D - Move | Space - Jump | R - Restart', 10, this.height - 10);
    }
    
    gameLoop() {
        this.update();
        this.render();
        
        // Диагностика - первые 3 кадра
        if (this.gameTime <= 3) {
            console.log(`📊 Frame ${this.gameTime}:`, {
                player: { x: this.player.x.toFixed(1), y: this.player.y.toFixed(1) },
                platforms: this.levelData.platforms?.length || 0,
                platformsSample: this.levelData.platforms?.[0]
            });
        }
        
        // Перезагрузка по R
        if (this.keys['r'] || this.keys['R']) {
            this.resetGame();
        }
        
        requestAnimationFrame(() => this.gameLoop());
    }
    
    resetGame() {
        this.player.health = 3;
        this.player.score = 0;
        this.collectedItems = [];
        this.gameOver = false;
        this.won = false;
        this.gameTime = 0;
        this.resetPlayerPosition();
    }
    
    start() {
        console.log('🎮 GameEngine.start() called - beginning game loop');
        this.gameLoop();
    }
}
