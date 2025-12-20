// CAD 文件查看器前端逻辑
class CadViewer {
    constructor() {
        this.uploadArea = document.getElementById('uploadArea');
        this.fileInput = document.getElementById('fileInput');
        this.loading = document.getElementById('loading');
        this.status = document.getElementById('status');
        this.fileInfo = document.getElementById('fileInfo');
        this.infoContent = document.getElementById('infoContent');
        this.convertBtn = document.getElementById('convertBtn');
        this.newFileBtn = document.getElementById('newFileBtn');
        this.apiUrlInput = document.getElementById('apiUrl');

        this.currentFile = null;
        this.fileData = null;

        this.initEventListeners();
    }

    initEventListeners() {
        // 点击上传区域
        this.uploadArea.addEventListener('click', () => {
            this.fileInput.click();
        });

        // 文件选择
        this.fileInput.addEventListener('change', (e) => {
            if (e.target.files.length > 0) {
                this.handleFile(e.target.files[0]);
            }
        });

        // 拖拽上传
        this.uploadArea.addEventListener('dragover', (e) => {
            e.preventDefault();
            this.uploadArea.classList.add('dragover');
        });

        this.uploadArea.addEventListener('dragleave', () => {
            this.uploadArea.classList.remove('dragover');
        });

        this.uploadArea.addEventListener('drop', (e) => {
            e.preventDefault();
            this.uploadArea.classList.remove('dragover');

            if (e.dataTransfer.files.length > 0) {
                this.handleFile(e.dataTransfer.files[0]);
            }
        });

        // 转换按钮
        this.convertBtn.addEventListener('click', () => {
            if (this.currentFile) {
                this.convertFile(this.currentFile);
            }
        });

        // 新文件按钮
        this.newFileBtn.addEventListener('click', () => {
            this.reset();
        });
    }

    async handleFile(file) {
        // 验证文件类型
        const validExtensions = ['.dwg', '.dxf'];
        const fileName = file.name.toLowerCase();
        const isValid = validExtensions.some(ext => fileName.endsWith(ext));

        if (!isValid) {
            this.showStatus('error', '❌ 只支持 DWG 和 DXF 文件！');
            return;
        }

        // 验证文件大小 (50MB)
        const maxSize = 50 * 1024 * 1024;
        if (file.size > maxSize) {
            this.showStatus('error', '❌ 文件太大！最大支持 50MB');
            return;
        }

        this.currentFile = file;
        this.showLoading(true);
        this.hideStatus();

        try {
            // 获取文件信息
            const info = await this.getFileInfo(file);
            this.showFileInfo(info);
            this.showStatus('success', `✅ 文件加载成功: ${file.name}`);
        } catch (error) {
            this.showStatus('error', `❌ 获取文件信息失败: ${error.message}`);
        } finally {
            this.showLoading(false);
        }
    }

    async getFileInfo(file) {
        const apiUrl = this.apiUrlInput.value.trim() || 'http://localhost:5000';
        const formData = new FormData();
        formData.append('file', file);

        const response = await fetch(`${apiUrl}/api/cad/info`, {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.detail || '服务器错误');
        }

        return await response.json();
    }

    async convertFile(file) {
        this.showLoading(true);
        this.hideStatus();

        try {
            const apiUrl = this.apiUrlInput.value.trim() || 'http://localhost:5000';
            const formData = new FormData();
            formData.append('file', file);

            const response = await fetch(`${apiUrl}/api/cad/convert?format=dxf&binary=false`, {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.detail || '转换失败');
            }

            // 下载文件
            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = file.name.replace(/\.(dwg|dxf)$/i, '') + '_converted.dxf';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            window.URL.revokeObjectURL(url);

            this.showStatus('success', `✅ 文件转换成功并已下载！`);

            // 现在可以将转换后的文件加载到 cad-viewer
            this.showStatus('info', `💡 提示: 转换后的 DXF 文件可以直接在 cad-viewer 中打开查看`);
        } catch (error) {
            this.showStatus('error', `❌ 转换失败: ${error.message}`);
        } finally {
            this.showLoading(false);
        }
    }

    showFileInfo(info) {
        const html = `
            <div class="info-row">
                <span class="info-label">文件名:</span>
                <span class="info-value">${info.fileName}</span>
            </div>
            <div class="info-row">
                <span class="info-label">文件大小:</span>
                <span class="info-value">${this.formatFileSize(info.fileSize)}</span>
            </div>
            <div class="info-row">
                <span class="info-label">CAD 版本:</span>
                <span class="info-value">${info.version}</span>
            </div>
            <div class="info-row">
                <span class="info-label">实体数量:</span>
                <span class="info-value">${info.entityCount}</span>
            </div>
            <div class="info-row">
                <span class="info-label">图层数量:</span>
                <span class="info-value">${info.layerCount}</span>
            </div>
            <div class="info-row">
                <span class="info-label">块数量:</span>
                <span class="info-value">${info.blockCount}</span>
            </div>
            <div class="info-row">
                <span class="info-label">单位:</span>
                <span class="info-value">${info.units}</span>
            </div>
        `;
        this.infoContent.innerHTML = html;
        this.fileInfo.style.display = 'block';
    }

    formatFileSize(bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
    }

    showStatus(type, message) {
        this.status.className = `status ${type}`;
        this.status.textContent = message;
        this.status.style.display = 'block';
    }

    hideStatus() {
        this.status.style.display = 'none';
    }

    showLoading(show) {
        this.loading.style.display = show ? 'block' : 'none';
    }

    reset() {
        this.currentFile = null;
        this.fileData = null;
        this.fileInput.value = '';
        this.fileInfo.style.display = 'none';
        this.hideStatus();
    }
}

// 初始化应用
document.addEventListener('DOMContentLoaded', () => {
    new CadViewer();
});
