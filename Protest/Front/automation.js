class Automation extends List {
	constructor(args) {
		super(args);

		this.args = args ?? {filter:"", find:"", sort:"", select:null};

		this.AddCssDependencies("list.css");

		const columns = ["name", "status", "progress"];
		this.SetupColumns(columns);

		this.columnsOptions.style.display = "none";

		this.SetTitle("Automation");
		this.SetIcon("mono/automation.svg");

		this.list.style.overflowY = "auto";

		this.SetupToolbar();
		this.createButton = this.AddToolbarButton("Create worker", "mono/add.svg?light");
		this.deleteButton = this.AddToolbarButton("Delete", "mono/delete.svg?light");
		this.toolbar.appendChild(this.AddToolbarSeparator());
		this.startButton = this.AddToolbarButton("Start", "mono/play.svg?light");
		this.pauseButton = this.AddToolbarButton("Pause", "mono/pause.svg?light");
		this.stopButton  = this.AddToolbarButton("Stop", "mono/stop.svg?light");

		this.createButton.onclick = () => this.EditDialog();

		this.UpdateAuthorization();

		this.deleteButton.disabled = true;
		this.startButton.disabled = true;
		this.pauseButton.disabled = true;
		this.stopButton.disabled = true;

		this.ListWorkers();
	}

	UpdateAuthorization() { //overrides
		super.UpdateAuthorization();
		this.createButton.disabled = !KEEP.authorization.includes("*") && !KEEP.authorization.includes("automation:write");
		this.deleteButton.disabled = !KEEP.authorization.includes("*") && !KEEP.authorization.includes("automation:write");
		this.startButton.disabled = !KEEP.authorization.includes("*") && !KEEP.authorization.includes("automation:write");
		this.pauseButton.disabled = !KEEP.authorization.includes("*") && !KEEP.authorization.includes("automation:write");
		this.stopButton.disabled = !KEEP.authorization.includes("*") && !KEEP.authorization.includes("automation:write");
	}

	async ListWorkers() {
		try {
			const response = await fetch("tasks/list");
			if (response.status !== 200) LOADER.HttpErrorHandler(response.status);

			const json = await response.json();
			if (json.error) throw (json.error);

			this.link = json;

			for (let task in this.link.data) {
				const element =  document.createElement("div");
				element.id = task;
				element.className = "list-element";
				this.list.appendChild(element);

				this.InflateElement(element, this.link.data[task]);

				element.addEventListener("click", event=>this.Entry_onclick(event));

				if (this.args.select && this.args.select === task) {
					this.selected = element;
					element.style.backgroundColor = "var(--clr-select)";
				}
			}
		}
		catch (ex) {
			this.ConfirmBox(ex, true, "mono/error.svg");
		}
	}

	EditDialog(entry=null) {
		const dialog = this.DialogBox("460px");
		if (dialog === null) return;

		const {okButton, innerBox} = dialog;

		okButton.value = entry ? "Save" : "Create";

		okButton.onclick = async ()=> {
			
			dialog.Close();
		};
	}

	InflateElement(element, entry) { //overrides
		let icon;
		switch (entry.name.v.toLowerCase()) {
		case "lifeline": icon = "mono/lifeline.svg"; break;
		case "lastseen": icon = "mono/lastseen.svg"; break;
		case "watchdog": icon = "mono/watchdog.svg"; break;
		case "issues"  : icon = "mono/issues.svg"; break;
		case "fetch"   : icon = "mono/fetch.svg"; break;
		default        : icon = "mono/task.svg"; break;
		}

		const iconBox = document.createElement("div");
		iconBox.className = "list-element-icon";
		iconBox.style.backgroundImage = `url(${icon})`;
		element.appendChild(iconBox);

		super.InflateElement(element, entry, null);

		if (!element.ondblclick) {
			element.ondblclick = event=> {
				event.stopPropagation();
				this.Entry_ondblclick(event);
			};
		}
	}

	Entry_onclick(event) {
		this.deleteButton.disabled = true;
		this.startButton.disabled = true;
		this.stopButton.disabled = true;

		if (!(this.args.select in this.link.data)) {
			return;
		}

		if (this.link.data[this.args.select].name.v.toLowerCase() === "lifeline" ||
			this.link.data[this.args.select].name.v.toLowerCase() === "watchdog" ||
			this.link.data[this.args.select].name.v.toLowerCase() === "fetch") {
			this.deleteButton.disabled = true;
		}
		else {
			//this.deleteButton.disabled = false; //TODO: <-
		}

		if (this.link.data[this.args.select].status.v.toLowerCase() === "stopped") {
			//this.startButton.disabled = false;
			this.stopButton.disabled = true;
		}
		else {
			this.startButton.disabled = true;
			//this.stopButton.disabled = false;
		}
	}

	Entry_ondblclick(event) {

	}
}