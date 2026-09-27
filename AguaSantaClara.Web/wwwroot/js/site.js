(() => {
	const sidebar = document.getElementById("appSidebar");
	const toggle = document.getElementById("sidebarToggle");
	const backdrop = document.getElementById("sidebarBackdrop");

	if (!sidebar || !toggle || !backdrop) return;

	const mobileQuery = window.matchMedia("(max-width: 767.98px)");
	const closeMobileMenu = () => {
		sidebar.classList.remove("is-open");
		backdrop.classList.remove("is-visible");
		toggle.setAttribute("aria-expanded", "false");
	};

	toggle.addEventListener("click", () => {
		if (mobileQuery.matches) {
			const isOpen = sidebar.classList.toggle("is-open");
			backdrop.classList.toggle("is-visible", isOpen);
			toggle.setAttribute("aria-expanded", String(isOpen));
			return;
		}

		const isCollapsed = document.body.classList.toggle("sidebar-collapsed");
		localStorage.setItem("asc-sidebar-collapsed", String(isCollapsed));
		toggle.setAttribute("aria-expanded", String(!isCollapsed));
	});

	backdrop.addEventListener("click", closeMobileMenu);
	document.addEventListener("keydown", event => {
		if (event.key === "Escape") closeMobileMenu();
	});
	mobileQuery.addEventListener("change", closeMobileMenu);

	if (localStorage.getItem("asc-sidebar-collapsed") === "true" && !mobileQuery.matches) {
		document.body.classList.add("sidebar-collapsed");
		toggle.setAttribute("aria-expanded", "false");
	} else {
		toggle.setAttribute("aria-expanded", "true");
	}
})();
