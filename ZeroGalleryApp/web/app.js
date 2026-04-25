'use strict';

const API_BASE = '/api';
const NO_ALBUM = -1;

const IMAGE_EXTENSIONS = new Set(['.png', '.jpg', '.bmp', '.gif', '.heic', '.ico', '.svg', '.tiff', '.webp']);
const VIDEO_EXTENSIONS = new Set(['.mov', '.mp4', '.avi', '.webm', '.wmv', '.mkv']);
const PREVIEW_ONLY_IMAGES = new Set(['.heic', '.tiff']);

const PLACEHOLDER_GALLERY = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjQwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMjQwIiBoZWlnaHQ9IjIwMCIgZmlsbD0iIzJhMmEyYSIvPjwvc3ZnPg==';
const PLACEHOLDER_ALBUM = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjAwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMjAwIiBoZWlnaHQ9IjIwMCIgZmlsbD0iIzFmMWYxZiIvPjx0ZXh0IHg9IjUwJSIgeT0iNTAlIiBmaWxsPSIjODg4IiBmb250LXNpemU9IjYwIiB0ZXh0LWFuY2hvcj0ibWlkZGxlIiBkb21pbmFudC1iYXNlbGluZT0ibWlkZGxlIj7wn5OBPC90ZXh0Pjwvc3ZnPg==';
const PLACEHOLDER_ERROR = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjQwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMjQwIiBoZWlnaHQ9IjIwMCIgZmlsbD0iIzFmMWYxZiIvPjx0ZXh0IHg9IjUwJSIgeT0iNTAlIiBmaWxsPSIjODg4IiBmb250LXNpemU9IjE4IiB0ZXh0LWFuY2hvcj0ibWlkZGxlIiBkb21pbmFudC1iYXNlbGluZT0ibWlkZGxlIj5GaWxlPC90ZXh0Pjwvc3ZnPg==';

const isImage = (ext) => !!ext && IMAGE_EXTENSIONS.has(ext.toLowerCase());
const isVideo = (ext) => !!ext && VIDEO_EXTENSIONS.has(ext.toLowerCase());
const isMedia = (ext) => isImage(ext) || isVideo(ext);

const TRANSLATIONS = {
    ru: {
        uploadToken: 'Токен загрузки',
        navigation: 'Навигация',
        filesWithoutAlbum: 'Файлы без альбома',
        albums: 'Альбомы',
        addAlbum: '+ Добавить альбом',
        emptyState: 'Здесь пока ничего нет',
        emptyStateHint: 'Загрузите файлы, чтобы начать',
        uploadAreaText: 'Перетащите файлы сюда или кликните для выбора',
        enterAccessToken: 'Введите токен доступа',
        albumAccessToken: 'Токен доступа к альбому',
        enterToken: 'Введите токен',
        login: 'Войти',
        createNewAlbum: 'Создать новый альбом',
        albumName: 'Название альбома *',
        albumNamePlaceholder: 'Например: Летний отпуск 2024',
        description: 'Описание',
        descriptionPlaceholder: 'Краткое описание альбома',
        accessToken: 'Токен доступа',
        accessTokenPlaceholder: 'Оставьте пустым для открытого доступа',
        allowRemoveData: 'Разрешить удаление данных',
        allowRemoveDataHint: 'Если отключено, файлы в альбоме нельзя будет удалить',
        create: 'Создать',
        cancel: 'Отмена',
        download: 'Скачать',
        share: 'Поделиться',
        delete: 'Удалить',
        menu: 'Меню',
        close: 'Закрыть',
        changeLanguage: 'Сменить язык',
        confirmDelete: 'Подтвердите удаление',
        confirmDeleteFile: 'Вы уверены, что хотите удалить этот файл?',
        confirmDeleteAlbum: 'Вы уверены, что хотите удалить этот альбом? Все файлы в альбоме также будут удалены.',
        enterUploadTokenMsg: 'Введите токен загрузки',
        uploadingFiles: 'Загрузка файлов...',
        filesUploaded: 'Файлы успешно загружены',
        wrongToken: 'Неверный токен',
        uploadError: 'Ошибка загрузки',
        albumCreated: 'Альбом создан',
        albumCreateError: 'Ошибка создания альбома',
        enterAlbumNameMsg: 'Введите название альбома',
        loadAlbumsError: 'Ошибка загрузки альбомов',
        loadAlbumDataError: 'Ошибка загрузки данных альбома',
        loadDataError: 'Ошибка загрузки данных',
        fileDownloaded: 'Файл загружен',
        downloadFileError: 'Ошибка загрузки файла',
        linkCopied: 'Ссылка скопирована в буфер обмена',
        copyLinkError: 'Не удалось скопировать ссылку',
        videoPlaybackError: 'Ваш браузер не поддерживает воспроизведение видео.',
        fileDeleted: 'Файл удален',
        albumDeleted: 'Альбом удален',
        deleteError: 'Ошибка удаления',
        notAuthorized: 'Недостаточно прав для удаления'
    },
    en: {
        uploadToken: 'Upload token',
        navigation: 'Navigation',
        filesWithoutAlbum: 'Files without album',
        albums: 'Albums',
        addAlbum: '+ Add album',
        emptyState: 'Nothing here yet',
        emptyStateHint: 'Upload files to get started',
        uploadAreaText: 'Drag files here or click to select',
        enterAccessToken: 'Enter access token',
        albumAccessToken: 'Album access token',
        enterToken: 'Enter token',
        login: 'Login',
        createNewAlbum: 'Create new album',
        albumName: 'Album name *',
        albumNamePlaceholder: 'E.g.: Summer vacation 2024',
        description: 'Description',
        descriptionPlaceholder: 'Brief album description',
        accessToken: 'Access token',
        accessTokenPlaceholder: 'Leave empty for public access',
        allowRemoveData: 'Allow data deletion',
        allowRemoveDataHint: 'If disabled, files in the album cannot be deleted',
        create: 'Create',
        cancel: 'Cancel',
        download: 'Download',
        share: 'Share',
        delete: 'Delete',
        menu: 'Menu',
        close: 'Close',
        changeLanguage: 'Change language',
        confirmDelete: 'Confirm deletion',
        confirmDeleteFile: 'Are you sure you want to delete this file?',
        confirmDeleteAlbum: 'Are you sure you want to delete this album? All files in the album will also be deleted.',
        enterUploadTokenMsg: 'Enter upload token',
        uploadingFiles: 'Uploading files...',
        filesUploaded: 'Files uploaded successfully',
        wrongToken: 'Invalid token',
        uploadError: 'Upload error',
        albumCreated: 'Album created',
        albumCreateError: 'Album creation error',
        enterAlbumNameMsg: 'Enter album name',
        loadAlbumsError: 'Error loading albums',
        loadAlbumDataError: 'Error loading album data',
        loadDataError: 'Error loading data',
        fileDownloaded: 'File downloaded',
        downloadFileError: 'File download error',
        linkCopied: 'Link copied to clipboard',
        copyLinkError: 'Failed to copy link',
        videoPlaybackError: 'Your browser does not support video playback.',
        fileDeleted: 'File deleted',
        albumDeleted: 'Album deleted',
        deleteError: 'Deletion error',
        notAuthorized: 'Insufficient permissions to delete'
    }
};

const safeStorage = (() => {
    try {
        const k = '__storage_test__';
        localStorage.setItem(k, k);
        localStorage.removeItem(k);
        return localStorage;
    } catch { return null; }
})();

class Gallery {
    constructor() {
        this.lang = (safeStorage?.getItem('language') === 'en') ? 'en' : 'ru';
        this.currentAlbumId = NO_ALBUM;
        this.albumTokens = Object.create(null);
        this.itemsById = new Map();
        this.albumsById = new Map();
        this.pendingAlbumId = null;
        this.deleteTarget = null;
        this.viewerZoom = 1;
        this.zoomInfoTimeout = null;
        this.mobileMenuOpen = false;
        this.dataAbort = null;
        this.lazyObserver = null;
        this.loadedImages = new Set();

        this.dom = this.cacheDom();
        this.init();
    }

    cacheDom() {
        const $ = (id) => document.getElementById(id);
        return {
            uploadToken: $('uploadToken'),
            languageSwitcher: $('languageSwitcher'),
            mobileMenuToggle: $('mobileMenuToggle'),
            sidebar: $('sidebar'),
            mainContent: $('mainContent'),
            noAlbumItem: $('noAlbumItem'),
            albumsList: $('albumsList'),
            galleryGrid: $('galleryGrid'),
            emptyState: $('emptyState'),
            uploadArea: $('uploadArea'),
            fileInput: $('fileInput'),
            addAlbumBtn: $('addAlbumBtn'),
            tokenModal: $('tokenModal'),
            albumTokenInput: $('albumTokenInput'),
            submitAlbumTokenBtn: $('submitAlbumTokenBtn'),
            addAlbumModal: $('addAlbumModal'),
            albumNameInput: $('albumNameInput'),
            albumDescriptionInput: $('albumDescriptionInput'),
            albumAccessTokenInput: $('albumAccessTokenInput'),
            albumAllowRemoveDataInput: $('albumAllowRemoveDataInput'),
            createAlbumBtn: $('createAlbumBtn'),
            deleteConfirmModal: $('deleteConfirmModal'),
            deleteConfirmText: $('deleteConfirmText'),
            confirmDeleteBtn: $('confirmDeleteBtn'),
            viewerModal: $('viewerModal'),
            viewerContent: $('viewerContent'),
            zoomInfo: $('zoomInfo'),
            galleryItemTpl: $('galleryItemTemplate'),
            albumTileTpl: $('albumTileTemplate')
        };
    }

    t(key) {
        return TRANSLATIONS[this.lang][key] ?? TRANSLATIONS.ru[key] ?? key;
    }

    init() {
        this.applyTranslations();
        this.attachListeners();
        this.applyViewportFix();
        this.loadAlbums();
        this.loadGalleryFor(NO_ALBUM);
    }

    applyTranslations() {
        const d = this.dom;
        document.querySelectorAll('[data-i18n]').forEach(el => {
            el.textContent = this.t(el.getAttribute('data-i18n'));
        });
        d.uploadToken.placeholder = this.t('uploadToken');
        d.albumTokenInput.placeholder = this.t('enterToken');
        d.albumNameInput.placeholder = this.t('albumNamePlaceholder');
        d.albumDescriptionInput.placeholder = this.t('descriptionPlaceholder');
        d.albumAccessTokenInput.placeholder = this.t('accessTokenPlaceholder');
        d.mobileMenuToggle.setAttribute('aria-label', this.t('menu'));
        d.languageSwitcher.setAttribute('aria-label', this.t('changeLanguage'));
        document.querySelector('#viewerCloseBtn').setAttribute('aria-label', this.t('close'));
        d.languageSwitcher.textContent = this.lang === 'ru' ? '🇬🇧' : '🇷🇺';
        document.documentElement.lang = this.lang;
    }

    toggleLanguage() {
        this.lang = this.lang === 'ru' ? 'en' : 'ru';
        safeStorage?.setItem('language', this.lang);
        this.applyTranslations();
        this.refreshActiveView();
    }

    refreshActiveView() {
        this.loadAlbums();
        this.loadGalleryFor(this.currentAlbumId);
    }

    /* ----------------- Listeners ----------------- */

    attachListeners() {
        const d = this.dom;

        d.languageSwitcher.addEventListener('click', () => this.toggleLanguage());
        d.addAlbumBtn.addEventListener('click', () => this.openModal('addAlbumModal'));
        d.noAlbumItem.addEventListener('click', () => this.selectAlbum(NO_ALBUM, false));

        // Generic close buttons (data-close="<modalId>")
        document.querySelectorAll('[data-close]').forEach(el => {
            el.addEventListener('click', () => this.closeModal(el.getAttribute('data-close')));
        });

        d.submitAlbumTokenBtn.addEventListener('click', () => this.submitAlbumToken());
        d.albumTokenInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') this.submitAlbumToken(); });

        d.createAlbumBtn.addEventListener('click', () => this.createAlbum());
        d.albumNameInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') this.createAlbum(); });

        d.confirmDeleteBtn.addEventListener('click', () => this.confirmDelete());

        // Gallery delegation
        d.galleryGrid.addEventListener('click', (e) => this.onGalleryClick(e));

        // Albums delegation
        d.albumsList.addEventListener('click', (e) => this.onAlbumsClick(e));

        // Mobile menu
        d.mobileMenuToggle.addEventListener('click', () => this.toggleMobileMenu());
        document.addEventListener('click', (e) => {
            if (this.mobileMenuOpen && !d.sidebar.contains(e.target) && !d.mobileMenuToggle.contains(e.target)) {
                this.closeMobileMenu();
            }
        });

        // Upload
        d.uploadArea.addEventListener('click', () => d.fileInput.click());
        d.uploadArea.addEventListener('dragover', (e) => { e.preventDefault(); d.uploadArea.classList.add('drag-over'); });
        d.uploadArea.addEventListener('dragleave', (e) => {
            if (!d.uploadArea.contains(e.relatedTarget)) d.uploadArea.classList.remove('drag-over');
        });
        d.uploadArea.addEventListener('drop', (e) => {
            e.preventDefault();
            d.uploadArea.classList.remove('drag-over');
            this.uploadFiles(e.dataTransfer.files);
        });
        d.fileInput.addEventListener('change', (e) => {
            this.uploadFiles(e.target.files);
            e.target.value = '';
        });

        // Modal backdrop click
        document.querySelectorAll('.modal').forEach(modal => {
            modal.addEventListener('click', (e) => {
                if (e.target === modal && modal.classList.contains('show')) {
                    this.closeModal(modal.id);
                }
            });
        });

        // Keyboard
        document.addEventListener('keydown', (e) => {
            if (e.key !== 'Escape') return;
            if (d.viewerModal.classList.contains('show')) { this.closeModal('viewerModal'); return; }
            const open = document.querySelector('.modal.show');
            if (open) this.closeModal(open.id);
            else if (this.mobileMenuOpen) this.closeMobileMenu();
        });

        // Viewer zoom
        d.viewerModal.addEventListener('wheel', (e) => this.onViewerWheel(e), { passive: false });

        // Lifecycle
        window.addEventListener('resize', () => this.applyViewportFix());
        window.addEventListener('orientationchange', () => this.applyViewportFix());
        window.addEventListener('beforeunload', () => this.lazyObserver?.disconnect());
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) this.loadedImages.clear();
        });
    }

    onGalleryClick(e) {
        const itemEl = e.target.closest('.gallery-item');
        if (!itemEl) return;
        const id = Number(itemEl.dataset.itemId);
        const item = this.itemsById.get(id);
        if (!item) return;

        const actionEl = e.target.closest('[data-action]');
        if (actionEl) {
            e.stopPropagation();
            switch (actionEl.dataset.action) {
                case 'download': this.downloadFile(item); break;
                case 'share': this.shareFile(item.id); break;
                case 'delete-item': this.requestDelete('file', item.id, item.name); break;
            }
            return;
        }

        if (isMedia(item.extension)) this.viewMedia(item);
    }

    onAlbumsClick(e) {
        const tile = e.target.closest('.album-tile');
        if (!tile) return;
        const id = Number(tile.dataset.albumId);
        const album = this.albumsById.get(id);
        if (!album) return;

        const actionEl = e.target.closest('[data-action]');
        if (actionEl?.dataset.action === 'delete-album') {
            e.stopPropagation();
            this.requestDelete('album', album.id, album.name);
            return;
        }

        this.selectAlbum(album.id, album.isProtected);
    }

    /* ----------------- Lazy loading ----------------- */

    initLazyObserver() {
        this.lazyObserver?.disconnect();
        const rowMargin = this.estimateRowHeight() * 3;
        this.lazyObserver = new IntersectionObserver((entries, obs) => {
            for (const entry of entries) {
                if (!entry.isIntersecting) continue;
                this.hydrateLazyImage(entry.target);
                obs.unobserve(entry.target);
            }
        }, { root: this.dom.mainContent, rootMargin: `${rowMargin}px 0px` });
    }

    estimateRowHeight() {
        const first = this.dom.galleryGrid.querySelector('.gallery-item');
        return first ? first.getBoundingClientRect().height : 300;
    }

    observeLazy(img) {
        if (!this.lazyObserver) this.initLazyObserver();
        this.lazyObserver.observe(img);
    }

    hydrateLazyImage(img) {
        const key = img.dataset.itemId;
        if (key && this.loadedImages.has(key)) return;
        const src = img.dataset.lazySrc;
        if (!src) return;
        const tmp = new Image();
        tmp.onload = () => {
            img.src = src;
            img.removeAttribute('data-lazy-src');
            if (key) this.loadedImages.add(key);
        };
        tmp.onerror = () => {
            img.src = PLACEHOLDER_ERROR;
            img.removeAttribute('data-lazy-src');
            if (key) this.loadedImages.add(key);
        };
        tmp.src = src;
    }

    /* ----------------- Albums ----------------- */

    async loadAlbums() {
        try {
            const res = await fetch(`${API_BASE}/albums`);
            const albums = await res.json();
            this.renderAlbums(albums);
        } catch {
            this.notify(this.t('loadAlbumsError'), 'error');
        }
    }

    renderAlbums(albums) {
        const list = this.dom.albumsList;
        list.replaceChildren();
        this.albumsById.clear();

        const tpl = this.dom.albumTileTpl;
        for (const album of albums) {
            this.albumsById.set(album.id, album);
            const node = tpl.content.firstElementChild.cloneNode(true);
            node.dataset.albumId = String(album.id);

            const lock = node.querySelector('.lock-icon');
            if (album.isProtected) lock.hidden = false;

            const delBtn = node.querySelector('[data-action="delete-album"]');
            delBtn.title = this.t('delete');

            const img = node.querySelector('.album-tile-image');
            img.alt = album.name ?? '';
            img.src = PLACEHOLDER_ALBUM;
            if (album.imagePreviewId > 0) {
                img.dataset.itemId = `album-${album.id}`;
                img.dataset.lazySrc = `${API_BASE}/preview/${album.imagePreviewId}`;
                this.observeLazy(img);
            }

            const name = node.querySelector('.album-tile-name');
            name.textContent = album.name ?? '';
            name.title = album.name ?? '';

            list.appendChild(node);
        }
        this.updateActiveAlbumHighlight();
    }

    updateActiveAlbumHighlight() {
        const d = this.dom;
        d.albumsList.querySelectorAll('.album-tile').forEach(t => t.classList.remove('active'));
        d.noAlbumItem.classList.remove('active');
        if (this.currentAlbumId === NO_ALBUM) {
            d.noAlbumItem.classList.add('active');
            return;
        }
        const tile = d.albumsList.querySelector(`[data-album-id="${this.currentAlbumId}"]`);
        tile?.classList.add('active');
    }

    selectAlbum(albumId, isProtected) {
        if (isProtected && !this.albumTokens[albumId]) {
            this.pendingAlbumId = albumId;
            this.openModal('tokenModal');
            return;
        }
        this.currentAlbumId = albumId;
        this.updateActiveAlbumHighlight();
        this.loadGalleryFor(albumId);
        if (window.innerWidth <= 1024) this.closeMobileMenu();
    }

    submitAlbumToken() {
        const value = this.dom.albumTokenInput.value;
        if (!value || this.pendingAlbumId === null) return;
        this.albumTokens[this.pendingAlbumId] = value;
        const albumId = this.pendingAlbumId;
        this.pendingAlbumId = null;
        this.dom.albumTokenInput.value = '';
        this.closeModal('tokenModal');
        this.selectAlbum(albumId, true);
    }

    async createAlbum() {
        const d = this.dom;
        const name = d.albumNameInput.value.trim();
        if (!name) {
            this.notify(this.t('enterAlbumNameMsg'), 'error');
            return;
        }
        const body = {
            name,
            description: d.albumDescriptionInput.value.trim(),
            token: d.albumAccessTokenInput.value.trim(),
            allowRemoveData: d.albumAllowRemoveDataInput.checked
        };
        try {
            const res = await fetch(`${API_BASE}/album`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'X-ZERO-UPLOAD-TOKEN': d.uploadToken.value },
                body: JSON.stringify(body)
            });
            if (!res.ok) {
                this.notify(this.t('albumCreateError'), 'error');
                return;
            }
            this.notify(this.t('albumCreated'), 'success');
            this.closeModal('addAlbumModal');
            d.albumNameInput.value = '';
            d.albumDescriptionInput.value = '';
            d.albumAccessTokenInput.value = '';
            d.albumAllowRemoveDataInput.checked = true;
            this.loadAlbums();
        } catch {
            this.notify(this.t('albumCreateError'), 'error');
        }
    }

    /* ----------------- Gallery ----------------- */

    async loadGalleryFor(albumId) {
        this.lazyObserver?.disconnect();
        this.lazyObserver = null;
        this.loadedImages.clear();
        this.dataAbort?.abort();
        this.dataAbort = new AbortController();

        try {
            const headers = {};
            const url = albumId === NO_ALBUM
                ? `${API_BASE}/data`
                : `${API_BASE}/album/${albumId}/data`;

            if (albumId !== NO_ALBUM && this.albumTokens[albumId]) {
                headers['X-ZERO-ACCESS-TOKEN'] = this.albumTokens[albumId];
            }

            const res = await fetch(url, { headers, signal: this.dataAbort.signal });
            if (res.status === 401) {
                delete this.albumTokens[albumId];
                this.pendingAlbumId = albumId;
                this.openModal('tokenModal');
                return;
            }
            const data = await res.json();
            this.renderGallery(data ?? []);
        } catch (err) {
            if (err?.name === 'AbortError') return;
            this.notify(albumId === NO_ALBUM ? this.t('loadDataError') : this.t('loadAlbumDataError'), 'error');
        }
    }

    renderGallery(items) {
        const d = this.dom;
        d.galleryGrid.replaceChildren();
        this.itemsById.clear();

        if (!items.length) {
            d.emptyState.style.display = 'flex';
            return;
        }
        d.emptyState.style.display = 'none';

        const tpl = d.galleryItemTpl;
        const frag = document.createDocumentFragment();
        for (const item of items) {
            this.itemsById.set(item.id, item);

            const node = tpl.content.firstElementChild.cloneNode(true);
            node.dataset.itemId = String(item.id);

            const img = node.querySelector('.gallery-item-preview');
            img.src = PLACEHOLDER_GALLERY;
            img.dataset.itemId = String(item.id);
            img.dataset.lazySrc = `${API_BASE}/preview/${item.id}`;
            img.alt = item.name ?? '';

            const nameEl = node.querySelector('.gallery-item-name');
            nameEl.textContent = item.name ?? '';
            nameEl.title = item.name ?? '';

            node.querySelector('[data-action="download"]').title = this.t('download');
            node.querySelector('[data-action="share"]').title = this.t('share');
            node.querySelector('[data-action="delete-item"]').title = this.t('delete');

            frag.appendChild(node);
        }
        d.galleryGrid.appendChild(frag);

        // Defer to next frame so layout is settled before observer math
        requestAnimationFrame(() => {
            this.initLazyObserver();
            d.galleryGrid.querySelectorAll('.gallery-item-preview[data-lazy-src]').forEach(img => {
                this.lazyObserver.observe(img);
            });
        });
    }

    /* ----------------- Viewer ----------------- */

    viewMedia(item) {
        const content = this.dom.viewerContent;
        content.replaceChildren();
        this.viewerZoom = 1;

        if (isImage(item.extension)) {
            const needsPreview = PREVIEW_ONLY_IMAGES.has(item.extension.toLowerCase());
            const url = needsPreview
                ? `${API_BASE}/preview/${item.id}`
                : `${API_BASE}/data/${item.id}`;
            const img = document.createElement('img');
            img.src = url;
            img.alt = item.name ?? '';
            content.appendChild(img);
        } else if (isVideo(item.extension)) {
            const ext = item.extension.toLowerCase();
            let mime = item.mimeType;
            if (ext === '.wmv') mime = 'video/x-ms-wmv';
            else if (ext === '.avi') mime = 'video/x-msvideo';

            const video = document.createElement('video');
            video.controls = true;
            video.autoplay = true;
            const source = document.createElement('source');
            source.src = `${API_BASE}/data/${item.id}`;
            source.type = mime ?? '';
            video.appendChild(source);
            video.appendChild(document.createTextNode(this.t('videoPlaybackError')));
            content.appendChild(video);
        }

        this.openModal('viewerModal');
    }

    onViewerWheel(e) {
        e.preventDefault();
        const img = this.dom.viewerContent.querySelector('img');
        if (!img) return;
        const delta = e.deltaY > 0 ? -0.1 : 0.1;
        const next = Math.max(0.1, Math.min(5, this.viewerZoom + delta));
        if (next === this.viewerZoom) return;
        this.viewerZoom = next;
        img.style.transform = `scale(${next})`;
        const zi = this.dom.zoomInfo;
        zi.textContent = `${Math.round(next * 100)}%`;
        zi.classList.add('show');
        clearTimeout(this.zoomInfoTimeout);
        this.zoomInfoTimeout = setTimeout(() => zi.classList.remove('show'), 1000);
    }

    /* ----------------- Files: download / share / upload ----------------- */

    async downloadFile(item) {
        try {
            const headers = {};
            if (this.currentAlbumId !== NO_ALBUM && this.albumTokens[this.currentAlbumId]) {
                headers['X-ZERO-ACCESS-TOKEN'] = this.albumTokens[this.currentAlbumId];
            }
            const res = await fetch(`${API_BASE}/data/${item.id}`, { headers });
            if (!res.ok) throw new Error('download failed');

            const blob = await res.blob();
            const url = URL.createObjectURL(blob);
            try {
                const a = document.createElement('a');
                a.href = url;
                a.download = item.name ?? `file-${item.id}`;
                document.body.appendChild(a);
                a.click();
                a.remove();
            } finally {
                URL.revokeObjectURL(url);
            }
            this.notify(this.t('fileDownloaded'), 'success');
        } catch {
            this.notify(this.t('downloadFileError'), 'error');
        }
    }

    async shareFile(fileId) {
        const url = `${window.location.origin}${API_BASE}/data/${fileId}`;
        try {
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(url);
                this.notify(this.t('linkCopied'), 'success');
                return;
            }
            const ta = document.createElement('textarea');
            ta.value = url;
            ta.style.position = 'fixed';
            ta.style.left = '-9999px';
            document.body.appendChild(ta);
            ta.select();
            try {
                document.execCommand('copy');
                this.notify(this.t('linkCopied'), 'success');
            } catch {
                this.notify(this.t('copyLinkError'), 'error');
            } finally {
                ta.remove();
            }
        } catch {
            this.notify(this.t('copyLinkError'), 'error');
        }
    }

    async uploadFiles(fileList) {
        const files = Array.from(fileList ?? []);
        if (!files.length) return;

        const uploadToken = this.dom.uploadToken.value;
        if (!uploadToken) {
            this.notify(this.t('enterUploadTokenMsg'), 'error');
            return;
        }

        const fd = new FormData();
        for (const f of files) fd.append('files', f);

        const headers = { 'X-ZERO-UPLOAD-TOKEN': uploadToken };
        if (this.currentAlbumId !== NO_ALBUM && this.albumTokens[this.currentAlbumId]) {
            headers['X-ZERO-ACCESS-TOKEN'] = this.albumTokens[this.currentAlbumId];
        }

        const url = this.currentAlbumId !== NO_ALBUM
            ? `${API_BASE}/upload/${this.currentAlbumId}`
            : `${API_BASE}/upload`;

        this.notify(this.t('uploadingFiles'), 'success');
        try {
            const res = await fetch(url, { method: 'POST', headers, body: fd });
            if (res.ok) {
                this.notify(this.t('filesUploaded'), 'success');
                this.loadGalleryFor(this.currentAlbumId);
            } else if (res.status === 401) {
                this.notify(this.t('wrongToken'), 'error');
            } else {
                this.notify(this.t('uploadError'), 'error');
            }
        } catch {
            this.notify(this.t('uploadError'), 'error');
        }
    }

    /* ----------------- Delete ----------------- */

    requestDelete(type, id, name) {
        this.deleteTarget = { type, id, name };
        this.dom.deleteConfirmText.textContent = type === 'album'
            ? this.t('confirmDeleteAlbum')
            : this.t('confirmDeleteFile');
        this.openModal('deleteConfirmModal');
    }

    async confirmDelete() {
        const target = this.deleteTarget;
        this.closeModal('deleteConfirmModal');
        if (!target) return;
        this.deleteTarget = null;

        const uploadToken = this.dom.uploadToken.value;
        if (!uploadToken) {
            this.notify(this.t('enterUploadTokenMsg'), 'error');
            return;
        }

        const headers = { 'X-ZERO-UPLOAD-TOKEN': uploadToken };
        if (this.currentAlbumId !== NO_ALBUM && this.albumTokens[this.currentAlbumId]) {
            headers['X-ZERO-ACCESS-TOKEN'] = this.albumTokens[this.currentAlbumId];
        }
        const endpoint = target.type === 'album'
            ? `${API_BASE}/album/${target.id}`
            : `${API_BASE}/data/${target.id}`;

        try {
            const res = await fetch(endpoint, { method: 'DELETE', headers });
            if (res.ok) {
                if (target.type === 'album') {
                    this.notify(this.t('albumDeleted'), 'success');
                    if (this.currentAlbumId === target.id) {
                        this.currentAlbumId = NO_ALBUM;
                        this.updateActiveAlbumHighlight();
                        this.loadGalleryFor(NO_ALBUM);
                    }
                    this.loadAlbums();
                } else {
                    this.notify(this.t('fileDeleted'), 'success');
                    this.loadGalleryFor(this.currentAlbumId);
                }
            } else if (res.status === 401) {
                this.notify(this.t('notAuthorized'), 'error');
            } else {
                this.notify(this.t('deleteError'), 'error');
            }
        } catch {
            this.notify(this.t('deleteError'), 'error');
        }
    }

    /* ----------------- Modals ----------------- */

    openModal(id) {
        const modal = document.getElementById(id);
        if (!modal) return;
        modal.classList.add('show');
        document.body.style.overflow = 'hidden';
        const firstInput = modal.querySelector('input');
        if (firstInput) setTimeout(() => firstInput.focus(), 100);
    }

    closeModal(id) {
        const modal = document.getElementById(id);
        if (!modal) return;
        modal.classList.remove('show');
        document.body.style.overflow = '';

        if (id === 'viewerModal') {
            this.viewerZoom = 1;
            this.dom.zoomInfo.classList.remove('show');
            const video = this.dom.viewerContent.querySelector('video');
            if (video) {
                video.pause();
                video.removeAttribute('src');
                video.load();
            }
            this.dom.viewerContent.replaceChildren();
        }
    }

    /* ----------------- Mobile menu ----------------- */

    toggleMobileMenu() {
        if (this.mobileMenuOpen) this.closeMobileMenu();
        else this.openMobileMenu();
    }

    openMobileMenu() {
        this.mobileMenuOpen = true;
        this.dom.sidebar.classList.add('active');
        this.dom.mobileMenuToggle.textContent = '✕';
        document.body.classList.add('sidebar-open');
    }

    closeMobileMenu() {
        this.mobileMenuOpen = false;
        this.dom.sidebar.classList.remove('active');
        this.dom.mobileMenuToggle.textContent = '☰';
        document.body.classList.remove('sidebar-open');
    }

    /* ----------------- Misc ----------------- */

    applyViewportFix() {
        document.documentElement.style.setProperty('--vh', `${window.innerHeight * 0.01}px`);
    }

    notify(message, type = 'success') {
        const el = document.createElement('div');
        el.className = `notification ${type}`;
        el.textContent = message;
        document.body.appendChild(el);
        setTimeout(() => {
            el.style.animation = 'slideInRight 0.3s ease reverse';
            setTimeout(() => el.remove(), 300);
        }, 3000);
    }
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => new Gallery());
} else {
    new Gallery();
}
