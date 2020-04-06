
(function initSettings() {
    sidemenu_dynamicicon    = localStorage.getItem("dynamic_search_icon") === "true";
    $w.always_maxxed        = localStorage.getItem("w_always_maxed") === "true";
    document.body.className = localStorage.getItem("disable_anime") === "true" ? "disable-animation" : "";

    if (localStorage.getItem("zoom"))
        document.body.style.zoom = 75 + localStorage.getItem("zoom") * 5 + "%";
})();

class Settings extends Window {
    constructor() {
        super([224,224,224]);
        this.setTitle("Settings");
        this.setIcon("res/tool02.svgz");

        //this.win.style.backdropFilter = "blur(6px)";
        //this.win.style.backgroundColor = "rgba(32,32,32,.8)";
        //this.content.style.backgroundColor = "rgba(0,0,0,0)";

        this.content.style.padding = "24px";
        this.content.style.overflowY = "auto";

        this.chkDynamicSearchIcon = document.createElement("input");
        this.chkDynamicSearchIcon.type = "checkbox";
        this.chkDynamicSearchIcon.checked = true;
        this.content.appendChild(this.chkDynamicSearchIcon);
        this.AddCheckBoxLabel(this.content, this.chkDynamicSearchIcon, "Dynamic search icon");

        this.content.appendChild(document.createElement("br"));
        this.content.appendChild(document.createElement("br"));

        this.chkWinMaxxed = document.createElement("input");
        this.chkWinMaxxed.type = "checkbox";
        this.chkWinMaxxed.checked = true;
        this.content.appendChild(this.chkWinMaxxed);
        this.AddCheckBoxLabel(this.content, this.chkWinMaxxed, "Always maximize windows");

        this.content.appendChild(document.createElement("br"));
        this.content.appendChild(document.createElement("br"));

        this.chkDisableAnime = document.createElement("input");
        this.chkDisableAnime.type = "checkbox";
        this.chkDisableAnime.checked = true;
        this.content.appendChild(this.chkDisableAnime);
        this.AddCheckBoxLabel(this.content, this.chkDisableAnime, "Disable animations");

        this.content.appendChild(document.createElement("br"));
        this.content.appendChild(document.createElement("br"));

        let divZoom = document.createElement("div");
        divZoom.innerHTML = "Zoom: ";
        divZoom.style.display = "inline-block";
        divZoom.style.minWidth = "200px";
        this.content.appendChild(divZoom);

        this.zoom = document.createElement("input");
        this.zoom.type = "range";
        this.zoom.min = "1";
        this.zoom.max = "15";
        this.zoom.value = "5";
        this.zoom.style.width = "200px";
        this.content.appendChild(this.zoom);

        let divZoomValue = document.createElement("div");
        divZoomValue.innerHTML = "100%";
        divZoomValue.style.paddingLeft = "8px";
        divZoomValue.style.display = "inline-block";
        this.content.appendChild(divZoomValue);

        this.content.appendChild(document.createElement("br"));
        this.content.appendChild(document.createElement("br"));
        this.content.appendChild(document.createElement("hr"));
        this.content.appendChild(document.createElement("br"));
        
        this.chkAliveOnClose = document.createElement("input");
        this.chkAliveOnClose.type = "checkbox";
        this.chkAliveOnClose.checked = true;
        this.content.appendChild(this.chkAliveOnClose);
        this.AddCheckBoxLabel(this.content, this.chkAliveOnClose, "Keep session alive when browser is closed");

        this.content.appendChild(document.createElement("br"));
        this.content.appendChild(document.createElement("br"));

        let divSessionTimeout = document.createElement("div");
        divSessionTimeout.innerHTML = "Logout if inactive for: ";
        divSessionTimeout.style.display = "inline-block";
        divSessionTimeout.style.minWidth = "200px";
        this.content.appendChild(divSessionTimeout);

        this.sessionTimeout = document.createElement("input");
        this.sessionTimeout.type = "range";
        this.sessionTimeout.min = "1";
        this.sessionTimeout.max = "8";
        this.sessionTimeout.value = "1";
        this.sessionTimeout.style.width = "200px";
        this.content.appendChild(this.sessionTimeout);

        let divSessionTimeoutValue = document.createElement("div");
        divSessionTimeoutValue.innerHTML = "15 min.";
        divSessionTimeoutValue.style.paddingLeft = "8px";
        divSessionTimeoutValue.style.display = "inline-block";
        this.content.appendChild(divSessionTimeoutValue);

        this.content.appendChild(document.createElement("br"));
        this.content.appendChild(document.createElement("br"));
        this.content.appendChild(document.createElement("hr"));
        this.content.appendChild(document.createElement("br"));

        let btnClearLocalCache = document.createElement("input");
        btnClearLocalCache.type = "button";
        btnClearLocalCache.value = "Clear local cache";
        btnClearLocalCache.style.height = "36px";
        btnClearLocalCache.style.padding = "8px";
        this.content.appendChild(btnClearLocalCache);

        this.content.appendChild(document.createElement("br"));
        this.content.appendChild(document.createElement("br"));

        let divButtons = document.createElement("div");
        divButtons.style.textAlign = "center";
        this.content.appendChild(divButtons);

        let btnOK = document.createElement("input");
        btnOK.type = "button";
        btnOK.value = "Apply";
        divButtons.appendChild(btnOK);

        let btnClose = document.createElement("input");
        btnClose.type = "button";
        btnClose.value = "Close";
        divButtons.appendChild(btnClose);

        this.LoadSettings();

        this.zoom.oninput = () => { divZoomValue.innerHTML = 75 + this.zoom.value * 5 + "%"; };

        this.sessionTimeout.oninput = () => {
            let timeMapping = { 1:15, 2:30, 3:60, 4:2*60, 5:4*60, 6:8*60, 7:24*60, 8:Infinity };
            if (timeMapping[this.sessionTimeout.value] == Infinity) {
                divSessionTimeoutValue.innerHTML = timeMapping[this.sessionTimeout.value];
            } else {
                let value = timeMapping[this.sessionTimeout.value];
                divSessionTimeoutValue.innerHTML = value > 60 ? value / 60 + " hours" : value + " minutes";
            }
        };

        btnClearLocalCache.onclick = ()=> { this.ClearCache(); };
        btnOK.onclick = ()=> { this.Apply(); };
        btnClose.onclick = ()=> { this.Close(); };

        this.sessionTimeout.oninput();
        this.zoom.oninput();
    }

    ClearCache() {
        this.ConfirmBox("Are you sure you want rebuild local cache? This will refresh this page.", false).addEventListener("click", () => {
            localStorage.clear();
            location.reload();
        });
    }

    LoadSettings() {
        this.chkDynamicSearchIcon.checked = localStorage.getItem("dynamic_search_icon") === "true";
        this.chkWinMaxxed.checked         = localStorage.getItem("w_always_maxed") === "true";
        this.chkDisableAnime.checked      = localStorage.getItem("disable_anime") === "true";
        this.zoom.value                   = localStorage.getItem("zoom") == null ? 5 : parseInt(localStorage.getItem("zoom"));
        this.chkAliveOnClose.checked      = localStorage.getItem("alive_after_close") === "true";
        this.sessionTimeout.value         = localStorage.getItem("session_timeout") == null ? 1 : parseInt(localStorage.getItem("session_timeout"));
    }

    Apply() {
        sidemenu_dynamicicon     = this.chkDynamicSearchIcon.checked;
        $w.always_maxxed         = this.chkWinMaxxed.checked;
        document.body.className  = this.chkDisableAnime.checked ? "disable-animation" : "";
        document.body.style.zoom = 75 + this.zoom.value * 5 + "%";

        localStorage.setItem("dynamic_search_icon", this.chkDynamicSearchIcon.checked);
        localStorage.setItem("w_always_maxed", this.chkWinMaxxed.checked);
        localStorage.setItem("disable_anime", this.chkDisableAnime.checked);
        localStorage.setItem("zoom", this.zoom.value);
        localStorage.setItem("alive_after_close", this.chkAliveOnClose.checked);
        localStorage.setItem("session_timeout", this.sessionTimeout.value);
    }

}
