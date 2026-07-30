/* ============================================================
   ESTAFF — CLIP picker, classification toggle, status dialog

   Progressive enhancement only: every control here wraps a real form
   element that already works on its own. If this file fails to load the
   pages still submit, they are just less pleasant.
============================================================ */
(function () {
    'use strict';

    // ════════════════════════════════════════════
    // CLIP ITEM PICKER
    // ════════════════════════════════════════════

    // Most urgent first. Matches ClipService.SortByExpiry on the server.
    var URGENCY_ORDER = ['expired', 'soon', 'active', 'none'];

    var URGENCY_LABEL = {
        expired: 'Expired — action overdue',
        soon:    'Expiring soon',
        active:  'Active',
        none:    'No expiry date'
    };

    var URGENCY_ICON = {
        expired: 'fa-triangle-exclamation',
        soon:    'fa-clock',
        active:  'fa-circle-check',
        none:    'fa-circle-minus'
    };

    function ClipPicker(root) {
        this.root      = root;
        this.native    = root.querySelector('.clip-picker-native');
        this.trigger   = root.querySelector('.clip-picker-trigger');
        this.panel     = root.querySelector('.clip-picker-panel');
        this.search    = root.querySelector('.clip-picker-search');
        this.list      = root.querySelector('.clip-picker-list');
        this.clearBtn  = root.querySelector('.clip-picker-clear');

        if (!this.native || !this.trigger || !this.panel || !this.list) return;

        this.items       = readItems(root);
        this.activeIndex = -1;
        this.visible     = [];

        root.classList.add('is-enhanced');
        this.bind();
        this.render();
        this.syncTrigger();
    }

    // The server renders the item list as JSON in a <script type="application/json">
    // sibling so the markup stays valid and nothing has to be parsed out of
    // data-attributes on every option.
    function readItems(root) {
        var node = root.querySelector('.clip-picker-data');
        if (!node) return [];
        try {
            return JSON.parse(node.textContent || node.innerText || '[]') || [];
        } catch (e) {
            return [];
        }
    }

    ClipPicker.prototype.bind = function () {
        var self = this;

        this.trigger.addEventListener('click', function (e) {
            e.preventDefault();
            self.toggle();
        });

        this.trigger.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown' || e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                self.open();
            }
        });

        if (this.search) {
            this.search.addEventListener('input', function () {
                self.render();
            });
            this.search.addEventListener('keydown', function (e) {
                self.onListKey(e);
            });
        }

        this.list.addEventListener('keydown', function (e) {
            self.onListKey(e);
        });

        if (this.clearBtn) {
            this.clearBtn.addEventListener('click', function (e) {
                e.preventDefault();
                self.select('');
                self.close();
                self.trigger.focus();
            });
        }

        document.addEventListener('click', function (e) {
            if (!self.root.contains(e.target)) self.close();
        });

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && self.root.classList.contains('is-open')) {
                self.close();
                self.trigger.focus();
            }
        });

        // Keeps the enhanced view honest if anything sets the select directly.
        this.native.addEventListener('change', function () {
            self.syncTrigger();
        });
    };

    ClipPicker.prototype.onListKey = function (e) {
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            this.move(1);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            this.move(-1);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            var item = this.visible[this.activeIndex];
            if (item) {
                this.select(item.key);
                this.close();
                this.trigger.focus();
            }
        }
    };

    ClipPicker.prototype.move = function (delta) {
        if (!this.visible.length) return;

        this.activeIndex += delta;
        if (this.activeIndex < 0) this.activeIndex = this.visible.length - 1;
        if (this.activeIndex >= this.visible.length) this.activeIndex = 0;

        this.highlight();
    };

    ClipPicker.prototype.highlight = function () {
        var buttons = this.list.querySelectorAll('.clip-picker-option');

        for (var i = 0; i < buttons.length; i++) {
            var isActive = i === this.activeIndex;
            buttons[i].classList.toggle('is-active', isActive);
            if (isActive && buttons[i].scrollIntoView) {
                buttons[i].scrollIntoView({ block: 'nearest' });
            }
        }

        this.trigger.setAttribute('aria-activedescendant',
            this.activeIndex >= 0 && buttons[this.activeIndex]
                ? buttons[this.activeIndex].id
                : '');
    };

    ClipPicker.prototype.toggle = function () {
        if (this.root.classList.contains('is-open')) this.close();
        else this.open();
    };

    ClipPicker.prototype.open = function () {
        if (this.trigger.disabled) return;

        this.root.classList.add('is-open');
        this.trigger.setAttribute('aria-expanded', 'true');

        if (this.search) {
            this.search.value = '';
            this.render();
            this.search.focus();
        }
    };

    ClipPicker.prototype.close = function () {
        this.root.classList.remove('is-open');
        this.trigger.setAttribute('aria-expanded', 'false');
        this.activeIndex = -1;
    };

    ClipPicker.prototype.setItems = function (items) {
        this.items = items || [];

        // Rebuild the underlying <select> so a non-JS submit still posts a
        // valid key and any stale selection is dropped.
        var previous = this.native.value;
        while (this.native.options.length > 1) this.native.remove(1);

        for (var i = 0; i < this.items.length; i++) {
            var item = this.items[i];
            var opt = document.createElement('option');
            opt.value = item.key;
            opt.textContent = item.kind + ' · ' + item.title +
                ' — ' + item.status + ' · ' + item.expiryText;
            this.native.appendChild(opt);
        }

        this.native.value = this.hasKey(previous) ? previous : '';
        this.render();
        this.syncTrigger();
    };

    ClipPicker.prototype.hasKey = function (key) {
        if (!key) return false;
        for (var i = 0; i < this.items.length; i++) {
            if (this.items[i].key === key) return true;
        }
        return false;
    };

    ClipPicker.prototype.select = function (key) {
        this.native.value = key || '';

        // Let anything listening (e.g. validation) know it changed.
        if (typeof Event === 'function') {
            this.native.dispatchEvent(new Event('change', { bubbles: true }));
        } else {
            this.syncTrigger();
        }
    };

    ClipPicker.prototype.find = function (key) {
        for (var i = 0; i < this.items.length; i++) {
            if (this.items[i].key === key) return this.items[i];
        }
        return null;
    };

    ClipPicker.prototype.syncTrigger = function () {
        var title = this.trigger.querySelector('.clip-picker-trigger-title');
        var sub   = this.trigger.querySelector('.clip-picker-trigger-sub');
        var icon  = this.trigger.querySelector('.clip-picker-trigger-icon');
        var item  = this.find(this.native.value);

        this.trigger.disabled = this.items.length === 0;

        if (!item) {
            title.textContent = this.items.length
                ? 'Select a CLIP item…'
                : 'No CLIP items for this employee';
            title.classList.add('is-placeholder');
            sub.textContent = '';
            sub.style.color = '';
            if (icon) icon.className = 'clip-picker-trigger-icon fas fa-list-ul clip-picker-caret';
            return;
        }

        title.textContent = item.kind + ' · ' + item.title;
        title.classList.remove('is-placeholder');

        sub.textContent = item.status + ' · ' + item.expiryText +
            (item.plant ? ' · ' + item.plant : '');
        sub.style.color = urgencyColor(item.urgency);
    };

    function urgencyColor(urgency) {
        if (urgency === 'expired') return '#991B1B';
        if (urgency === 'soon')    return '#92400E';
        if (urgency === 'active')  return '#065F46';
        return '';
    }

    ClipPicker.prototype.render = function () {
        var term = (this.search && this.search.value || '')
            .trim().toLowerCase();

        var matches = this.items.filter(function (item) {
            if (!term) return true;
            return [item.title, item.subtitle, item.plant,
                    item.kind, item.status, item.processStatus]
                .join(' ').toLowerCase().indexOf(term) !== -1;
        });

        this.visible = [];
        this.list.innerHTML = '';
        this.activeIndex = -1;

        if (!matches.length) {
            var empty = document.createElement('li');
            empty.className = 'clip-picker-empty';
            empty.textContent = this.items.length
                ? 'No CLIP items match “' + (this.search ? this.search.value : '') + '”.'
                : 'No COF or plant monitoring records are linked to these plants.';
            this.list.appendChild(empty);
            return;
        }

        var selected = this.native.value;
        var self = this;
        var index = 0;

        URGENCY_ORDER.forEach(function (urgency) {
            var group = matches.filter(function (i) {
                return i.urgency === urgency;
            });
            if (!group.length) return;

            self.list.appendChild(buildGroupHeading(urgency, group.length));

            group.forEach(function (item) {
                var li = document.createElement('li');
                li.appendChild(
                    buildOption(item, index, item.key === selected, self));
                self.list.appendChild(li);
                self.visible.push(item);
                index++;
            });
        });
    };

    function buildGroupHeading(urgency, count) {
        var li = document.createElement('li');
        li.className = 'clip-picker-group urgency-' + urgency;
        li.setAttribute('role', 'presentation');

        var icon = document.createElement('i');
        icon.className = 'fas ' + URGENCY_ICON[urgency];
        li.appendChild(icon);

        li.appendChild(document.createTextNode(URGENCY_LABEL[urgency]));

        var badge = document.createElement('span');
        badge.className = 'clip-picker-group-count';
        badge.textContent = count;
        li.appendChild(badge);

        return li;
    }

    function buildOption(item, index, isSelected, picker) {
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.id = 'clip-opt-' + Math.random().toString(36).slice(2, 9);
        btn.className = 'clip-picker-option urgency-' + item.urgency +
            (isSelected ? ' is-selected' : '');
        btn.setAttribute('role', 'option');
        btn.setAttribute('aria-selected', isSelected ? 'true' : 'false');

        var iconWrap = document.createElement('span');
        iconWrap.className = 'clip-picker-option-icon';
        var icon = document.createElement('i');
        icon.className = 'fas ' + (item.icon || 'fa-file');
        iconWrap.appendChild(icon);
        btn.appendChild(iconWrap);

        var body = document.createElement('span');
        body.className = 'clip-picker-option-body';

        var title = document.createElement('span');
        title.className = 'clip-picker-option-title';
        title.textContent = item.title;
        body.appendChild(title);

        var meta = document.createElement('span');
        meta.className = 'clip-picker-option-meta';
        meta.textContent = [item.kindLabel, item.subtitle, item.plant,
                            item.processStatus]
            .filter(Boolean).join(' · ');
        body.appendChild(meta);

        btn.appendChild(body);

        var expiry = document.createElement('span');
        expiry.className = 'clip-picker-option-expiry';
        expiry.appendChild(document.createTextNode(item.expiryText));
        var date = document.createElement('small');
        date.textContent = item.expiryDate;
        expiry.appendChild(date);
        btn.appendChild(expiry);

        btn.addEventListener('click', function (e) {
            e.preventDefault();
            picker.select(item.key);
            picker.close();
            picker.trigger.focus();
        });

        btn.addEventListener('mouseenter', function () {
            picker.activeIndex = index;
            picker.highlight();
        });

        return btn;
    }

    // ════════════════════════════════════════════
    // CLASSIFICATION → task type → CLIP picker
    // ════════════════════════════════════════════
    //
    // Choosing a classification narrows the task-type list to that
    // classification's own rows. Choosing CLIP hides the task type entirely and
    // reveals the CLIP picker instead, because picking a COF or plant
    // monitoring record is what decides the task type in that case.

    function wireClassificationField(field) {
        var radios    = field.querySelectorAll('.js-classification-radio');
        var listWrap  = field.querySelector('.task-list-field');
        var listInput = field.querySelector('#TaskListId');
        var clipWrap  = field.querySelector('.clip-field');
        var clipNative = field.querySelector('.clip-picker-native');

        if (!radios.length || !listInput) return;

        var clipId = field.getAttribute('data-clip-classification');
        var allLists = readJson(field.querySelector('.task-list-data'), []);
        var selectedList = readJson(field.querySelector('.task-list-selected'), null);

        function selected() {
            var checked = field.querySelector('.js-classification-radio:checked');
            return checked ? checked.value : '';
        }

        function apply() {
            var current = selected();
            var isClip = !!clipId && current === clipId;

            if (clipWrap) clipWrap.classList.toggle('is-visible', isClip);
            if (listWrap) listWrap.classList.toggle('is-hidden', isClip);

            // Rebuild the task-type list for this classification, keeping the
            // current choice only if it still belongs.
            var previous = listInput.value || (selectedList !== null
                ? String(selectedList) : '');

            while (listInput.options.length > 1) listInput.remove(1);

            var matches = allLists.filter(function (l) {
                return String(l.classification) === current;
            });

            matches.forEach(function (l) {
                var opt = document.createElement('option');
                opt.value = l.id;
                opt.textContent = l.name;
                listInput.appendChild(opt);
            });

            listInput.value =
                matches.some(function (l) { return String(l.id) === previous; })
                    ? previous
                    : '';

            listInput.disabled = matches.length === 0;

            // Drop a stale CLIP link the moment the task stops being CLIP work,
            // so it cannot be posted alongside another classification.
            if (!isClip && clipNative) {
                clipNative.value = '';
                if (clipNative.clipPicker) clipNative.clipPicker.syncTrigger();
            }
        }

        for (var i = 0; i < radios.length; i++) {
            radios[i].addEventListener('change', apply);
        }

        apply();
    }

    function readJson(node, fallback) {
        if (!node) return fallback;
        try {
            var parsed = JSON.parse(node.textContent || node.innerText || 'null');
            return parsed === null ? fallback : parsed;
        } catch (e) {
            return fallback;
        }
    }

    // ════════════════════════════════════════════
    // ASSIGNEE → reloads the CLIP list
    // ════════════════════════════════════════════

    function wireReload(root, picker) {
        var url       = root.getAttribute('data-reload-url');
        var triggerId = root.getAttribute('data-reload-trigger');
        if (!url || !triggerId) return;

        var select = document.getElementById(triggerId);
        if (!select) return;

        select.addEventListener('change', function () {
            var employeeId = select.value;

            if (!employeeId) {
                picker.setItems([]);
                return;
            }

            var req = new XMLHttpRequest();
            req.open('GET', url + '?employeeId=' +
                encodeURIComponent(employeeId), true);
            req.setRequestHeader('X-Requested-With', 'XMLHttpRequest');

            req.onload = function () {
                if (req.status < 200 || req.status >= 300) {
                    picker.setItems([]);
                    return;
                }
                try {
                    picker.setItems(JSON.parse(req.responseText));
                } catch (e) {
                    picker.setItems([]);
                }
            };

            req.onerror = function () { picker.setItems([]); };
            req.send();
        });
    }

    // ════════════════════════════════════════════
    // BOOT
    // ════════════════════════════════════════════

    function init() {
        var pickers = document.querySelectorAll('.clip-picker');

        for (var i = 0; i < pickers.length; i++) {
            var picker = new ClipPicker(pickers[i]);
            if (!picker.native) continue;

            picker.native.clipPicker = picker;
            wireReload(pickers[i], picker);
        }

        var fields = document.querySelectorAll('.classification-field');
        for (var j = 0; j < fields.length; j++) wireClassificationField(fields[j]);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
