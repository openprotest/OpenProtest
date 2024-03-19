class Wmi extends Window {
	constructor(params) {
		super();

		this.AddCssDependencies("wmi.css");

		this.params = params ?? { target: "", query: "" };

		this.SetTitle("WMI client");
		this.SetIcon("mono/wmi.svg");

		this.wmiClasses = {};
		this.GetWmiClasses();

		this.content.style.overflow = "hidden";

		const wmiInput = document.createElement("div");
		wmiInput.className = "wmi-input";
		this.content.appendChild(wmiInput);

		const targetLabel = document.createElement("div");
		targetLabel.style.gridArea = "1 / 1";
		targetLabel.textContent = "Target:";
		wmiInput.appendChild(targetLabel);

		this.targetInput = document.createElement("input");
		this.targetInput.type = "text";
		this.targetInput.placeholder = "hostname or ip";
		this.targetInput.style.gridArea = "1 / 2";
		if (this.params.target != null) this.targetInput.value = this.params.target;
		wmiInput.appendChild(this.targetInput);

		const targetButton = document.createElement("input");
		targetButton.type = "button";
		targetButton.value = "...";
		targetButton.style.gridArea = "2 / 3";
		wmiInput.appendChild(targetButton);

		const queryLabel = document.createElement("div");
		queryLabel.textContent = "Query:";
		queryLabel.style.gridArea = "2 / 1";
		wmiInput.appendChild(queryLabel);

		this.queryInput = document.createElement("textarea");
		this.queryInput.placeholder = "e.g.: SELECT * FROM Win32_BIOS WHERE Status = \"OK\"";
		this.queryInput.style.gridArea = "2 / 2 / 2 span / auto";
		//this.queryInput.style.fontFamily = "monospace";
		this.queryInput.style.resize = "none";
		if (this.params.query != null) this.queryInput.value = this.params.query;
		wmiInput.appendChild(this.queryInput);

		this.executeButton = document.createElement("input");
		this.executeButton.type = "button";
		this.executeButton.value = "Execute";
		this.executeButton.style.height = "auto";
		this.executeButton.style.gridArea = "3 / 3";
		wmiInput.appendChild(this.executeButton);

		const toggleButton = document.createElement("input");
		toggleButton.type = "button";
		toggleButton.className = "wmi-toggle-button";
		this.content.appendChild(toggleButton);

		this.plotBox = document.createElement("div");
		this.plotBox.className = "wmi-plot no-results";
		this.content.appendChild(this.plotBox);

		this.targetInput.oninput = ()=> { this.params.target = this.targetInput.value };
		this.queryInput.oninput = ()=> { this.params.query = this.queryInput.value };

		targetButton.onclick = ()=> this.SequelAssistant();

		this.executeButton.onclick = ()=> this.Query();

		toggleButton.onclick =()=> {
			if (wmiInput.style.visibility === "hidden") {
				toggleButton.style.top = "96px";
				toggleButton.style.transform = "rotate(-180deg)";
				wmiInput.style.visibility = "visible";
				wmiInput.style.opacity = "1";
				wmiInput.style.transform = "none";
				this.plotBox.style.top = "136px";
			}
			else {
				toggleButton.style.top = "0px";
				toggleButton.style.transform = "rotate(0deg)";
				wmiInput.style.visibility = "hidden";
				wmiInput.style.opacity = "0";
				wmiInput.style.transform = "translateY(-64px)";
				this.plotBox.style.top = "36px";
			}
		};

		if (this.params.target.length > 0 && this.params.query.length > 0) {
			this.executeButton.onclick();
			toggleButton.onclick();
		}
	}

	async GetWmiClasses() {
		try {
			const response = await fetch("wmiclasses.json");

			if (response.status !== 200) LOADER.HttpErrorHandler(response.status);

			const json = await response.json();
			if (json.error) throw(json.error);

			this.wmiClasses = json;
		}
		catch (ex) {
			this.ConfirmBox(ex, true, "mono/error.svg");
		}
	}

	SequelAssistant() {
		let lastQuery = this.queryInput.value.toLowerCase();

		let words = lastQuery.split(" ");
		let className = null;
		if (this.wmiClasses.classes) {
			for (let i = 0; i < words.length; i++) {
				if (words[i].toUpperCase() === "FROM" && i !== words.length-1) {
					className = words[i+1].toLowerCase();
					break;
				}
			}
		}

		let select_index = lastQuery.indexOf("select");
		let from_index = lastQuery.indexOf("from");
		let lastProperties = lastQuery.substring(select_index + 6, from_index).trim();
		let lastPropertiesArray = lastProperties.split(",").map(o=>o.trim());

		const dialog = this.DialogBox("640px");
		if (dialog === null) return;

		const okButton = dialog.okButton;
		const innerBox = dialog.innerBox;

		innerBox.style.margin = "16px";
		innerBox.style.display = "grid";
		innerBox.style.gridTemplateColumns = "50% 16px auto";
		innerBox.style.gridTemplateRows = "32px 8px auto 8px 64px";

		const classFilterInput = document.createElement("input");
		classFilterInput.type = "text";
		classFilterInput.placeholder = "Find..";
		classFilterInput.style.gridArea = "1 / 1";

		const noneButton = document.createElement("input");
		noneButton.type = "button";
		noneButton.style.position = "absolute";
		noneButton.style.right = "32px";
		noneButton.style.width = "28px";
		noneButton.style.minWidth = "28px";
		noneButton.style.backgroundColor = "transparent";
		noneButton.style.backgroundImage = "url(/mono/selectnone.svg)";
		noneButton.style.backgroundSize = "24px 24px";
		noneButton.style.backgroundPosition = "center";
		noneButton.style.backgroundRepeat = "no-repeat";

		const allButton = document.createElement("input");
		allButton.type = "button";
		allButton.style.position = "absolute";
		allButton.style.right = "0";
		allButton.style.width = "28px";
		allButton.style.minWidth = "28px";
		allButton.style.backgroundColor = "transparent";
		allButton.style.backgroundImage = "url(/mono/selectall.svg)";
		allButton.style.backgroundSize = "24px 24px";
		allButton.style.backgroundPosition = "center";
		allButton.style.backgroundRepeat = "no-repeat";

		innerBox.append(classFilterInput, noneButton, allButton);

		const classesList = document.createElement("div");
		classesList.className = "wmi-classes-list";
		classesList.style.border = "var(--clr-control) solid 1.5px";
		classesList.style.gridArea = "3 / 1";
		classesList.style.overflowY = "scroll";

		const propertiesList = document.createElement("div");
		propertiesList.className = "wmi-properties-list";
		propertiesList.style.border = "var(--clr-control) solid 1.5px";
		propertiesList.style.gridArea = "3 / 3";
		propertiesList.style.overflowY = "scroll";

		const previewInput = document.createElement("textarea");
		previewInput.setAttribute("readonly", true);
		previewInput.style.resize = "none";
		previewInput.style.gridArea = "5 / 1 / span 1 / span 3";

		innerBox.append(classesList, propertiesList, previewInput);

		if (!this.wmiClasses.classes) {
			this.ConfirmBox("Failed to load WMI classes.");
			okButton.onclick();
			return;
		}

		okButton.addEventListener("click", ()=> {
			this.queryInput.value = previewInput.value;
			this.params.query = this.queryInput.value;
		});

		classFilterInput.onkeydown = event=>{
			if (event.code === "Escape") {
				classFilterInput.value = "";
				classFilterInput.oninput()
			}
		};

		let selected = null;
		let properties = [];
		let propertyCheckboxes = [];

		classFilterInput.oninput = ()=> {
			if (!this.wmiClasses.classes) return;
			let filter = classFilterInput.value.toLowerCase();

			classesList.textContent = "";
			propertiesList.textContent = "";

			for (let i=0; i<this.wmiClasses.classes.length; i++) {
				let matched = false;

				if (this.wmiClasses.classes[i].class.toLowerCase().indexOf(filter) > -1) {
					matched = true;
				}
				else {
					for (let j = 0; j < this.wmiClasses.classes[i].properties.length; j++) {
						if (this.wmiClasses.classes[i].properties[j].toLowerCase().indexOf(filter) > -1) {
							matched = true;
							break;
						}
					}
				}

				if (matched) {
					const newClass = document.createElement("div");
					newClass.textContent = this.wmiClasses.classes[i].class;
					classesList.appendChild(newClass);

					newClass.onclick = ()=> {
						if (selected != null) selected.style.backgroundColor = "";

						properties = [];
						propertyCheckboxes = [];

						propertiesList.textContent = "";
						
						for (let j = 0; j < this.wmiClasses.classes[i].properties.length; j++) {
							let value = lastProperties === "*" || className == null ||
								className.toLowerCase() === this.wmiClasses.classes[i].class.toLowerCase() &&
								lastPropertiesArray.includes(this.wmiClasses.classes[i].properties[j].toLowerCase());

							const propertyBox = document.createElement("div");
							const propertyCheckbox = document.createElement("input");
							propertyCheckbox.type = "checkbox";
							propertyCheckbox.checked = value;
							propertyCheckboxes.push(propertyCheckbox);
							propertyBox.appendChild(propertyCheckbox);

							properties.push(value);

							propertyCheckbox.onchange = ()=> {
								properties[j] = propertyCheckbox.checked;

								let count = 0;
								for (let k = 0; k < properties.length; k++) {
									if (properties[k])
										count++;
								}

								if (count === 0 || count === properties.length) {
									previewInput.value = "SELECT * FROM " + this.wmiClasses.classes[i].class;
								}
								else {
									let sel = "";
									for (let k = 0; k < properties.length; k++)
										if (properties[k])
											sel += (sel.length == 0) ? this.wmiClasses.classes[i].properties[k] : ", " + this.wmiClasses.classes[i].properties[k];

									previewInput.value = "SELECT " + sel + " FROM " + this.wmiClasses.classes[i].class;
								}
							};

							this.AddCheckBoxLabel(propertyBox, propertyCheckbox, this.wmiClasses.classes[i].properties[j]);
							propertiesList.appendChild(propertyBox);

							if (filter && this.wmiClasses.classes[i].properties[j].toLowerCase().indexOf(filter) > -1) {
								propertyBox.scrollIntoView({ behavior: "smooth"});
								setTimeout(()=>{propertyBox.style.animation = "highlight .8s 1"}, 500);
							}

							selected = newClass;
							selected.style.backgroundColor = "var(--clr-select)";
						}

						previewInput.value = "SELECT * FROM " + this.wmiClasses.classes[i].class;
					};

					newClass.ondblclick = ()=> {
						this.queryInput.value = previewInput.value;
						okButton.onclick();
					};

					if (className && className === this.wmiClasses.classes[i].class.toLowerCase()) {
						newClass.onclick();
						newClass.scrollIntoView();
						className = null;
					}
				}
			}
		};
		classFilterInput.oninput();

		allButton.onclick = ()=> {
			if (propertyCheckboxes.length === 0) return;

			for (let i = 0; i < propertyCheckboxes.length; i++) {
				propertyCheckboxes[i].checked = true;
				properties[i] = true;
			}

			propertyCheckboxes[0].onchange();
		};

		noneButton.onclick = ()=> {
			if (propertyCheckboxes.length === 0) return;

			for (let i = 0; i < propertyCheckboxes.length; i++) {
				propertyCheckboxes[i].checked = false;
				properties[i] = false;
			}

			propertyCheckboxes[0].onchange();
		};
	}

	CallMethodDialog() {
		const dialog = this.DialogBox("640px");
		if (dialog === null) return;

		const okButton = dialog.okButton;

		okButton.addEventListener("click", ()=> {

		});
	}

	async Query() {
		if (this.targetInput.value.length == 0 || this.queryInput.value.length == 0) {
			this.ConfirmBox("Incomplete query.", true);
			return;
		}

		this.SetIcon("mono/wmi.svg");
		this.SetTitle("WMI client");

		const spinner = document.createElement("div");
		spinner.className = "spinner";
		spinner.style.textAlign = "left";
		spinner.style.marginTop = "160px";
		spinner.style.marginBottom = "32px";
		spinner.appendChild(document.createElement("div"));
		this.content.appendChild(spinner);

		this.targetInput.value = this.targetInput.value.trim();
		this.executeButton.disabled = true;
		this.plotBox.style.display = "none";
		this.plotBox.textContent = "";

		try {
			const response = await fetch(`wmi/query?target=${encodeURIComponent(this.targetInput.value)}`, {
				method: "POST",
				body: this.queryInput.value.trim().replaceAll("\n", " ")
			});

			if (response.status !== 200) LOADER.HttpErrorHandler(response.status);

			const text = await response.text();
			let split = text.split(String.fromCharCode(127));
			if (split.length > 1) this.Plot(split);
		}
		catch (ex) {
			this.ConfirmBox(ex, true, "mono/error.svg");
		}
		finally {
			this.executeButton.disabled = false;
			this.plotBox.style.display = "block";
			this.content.removeChild(spinner);
		}
	}

	Plot(split) {
		let words = this.queryInput.value.split(" ").map(v=> v.toLowerCase());
		let className = "";
		let hasMethods = false;
		let targetHost = this.targetInput.value;

		if (this.wmiClasses.classes) {
			for (let i = 0; i < words.length; i++)
				if (words[i].startsWith("win32_")) {
					className = words[i];
					break;
				}

			for (let i = 0; i < this.wmiClasses.classes.length; i++)
				if (this.wmiClasses.classes[i].class.toLowerCase().indexOf(className) > -1) {
					hasMethods = this.wmiClasses.classes[i].methods;
					break;
				}
		}

		const table = document.createElement("table");
		table.className = "wmi-table";

		let length = parseInt(split[0]);
		let unique = -1; //unique id position
		for (let i = 1; i < length + 1; i++)
			if (className == "win32_process" && split[i] == "ProcessId") {
				unique = i - 1;
				break;
			}
			/*else if (className == "win32_service" && split[i] == "Name") {
				unique = i - 1;
				break;
			}*/

		for (let i = 1; i < split.length - 1; i += length) {
			const tr = document.createElement("tr");
			table.appendChild(tr);

			const tdn = document.createElement("td");
			tr.appendChild(tdn);

			for (let j = 0; j < length; j++) {
				let td = document.createElement("td");
				td.textContent = split[i + j];
				tr.appendChild(td);
			}

			if (hasMethods && unique > -1) {
				let td = document.createElement("td");
				tr.appendChild(td);

				if (i > length) {
					switch (className) {
					case "win32_process":
						const terminateButton = document.createElement("input");
						terminateButton.type = "button";
						terminateButton.value = "Terminate";
						terminateButton.setAttribute("pid", split[i + unique]);
						td.appendChild(terminateButton);

						terminateButton.onclick = async event=> {
							terminateButton.disabled = true;
							let pid = event.target.getAttribute("pid");

							try {
								const response = await fetch(`wmi/killprocess?target=${encodeURIComponent(targetHost)}&pid=${pid}`);
								if (response.status !== 200) return;
								const text = await response.text();

								if (text === "ok")
									table.removeChild(tr);
								else {
									td.removeChild(terminateButton);
									td.textContent = text;
								}
							}
							catch (ex) {
								this.ConfirmBox(ex, true, "mono/error.svg");
							}
						};
						break;

					/*default:
						let methodButton = document.createElement("input");
						methodButton.type  = "button";
						methodButton.value = "Method";
						td.appendChild(methodButton);
						methodButton.onclick = ()=> {
							this.CallMethodDialog();
						};*/
					}
				}
			}
		}

		this.plotBox.appendChild(table);
	}
}