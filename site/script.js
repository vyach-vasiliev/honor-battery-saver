const profiles = {
  home: { title: { ru: "Дом", en: "Home", de: "Zuhause", pt: "Casa", ko: "집", zh: "居家" }, network: "HOME_5G", start: 40, end: 70 },
  office: { title: { ru: "Офис", en: "Office", de: "Büro", pt: "Escritório", ko: "사무실", zh: "办公" }, network: "STUDIO_WIFI", start: 70, end: 90 },
  travel: { title: { ru: "Поездка", en: "Travel", de: "Reise", pt: "Viagem", ko: "여행", zh: "出行" }, network: "AIRPORT_FREE", start: 95, end: 100 }
};

const languageContent = {
  ru: {
    title: "Honor Battery Saver — помогите HONOR сохранить батарею",
    description: "Сокращайте ненужное время на 100%: Honor Battery Saver автоматически выбирает лимиты 70%, 90% или 100% для ноутбука HONOR. Открытый код, без телеметрии.",
    ogDescription: "Бережная зарядка для ноутбуков HONOR с авто-переключением профилей.",
    languageLabel: "Язык интерфейса",
    navigationLabel: "Основная навигация",
    visualLabel: "Демонстрация переключения профилей зарядки",
    tabsLabel: "Выберите профиль для демонстрации",
    trustLabel: "Ключевые преимущества",
    comparisonLabel: "Сравнение обычной и бережной зарядки",
    brandLabel: "Honor Battery Saver — главная"
  },
  en: {
    title: "Honor Battery Saver — help your HONOR battery last longer",
    description: "Reduce unnecessary time at 100% with automatic 70%, 90%, and 100% charging profiles for compatible HONOR laptops. Open source, with no telemetry.",
    ogDescription: "Gentle charging for HONOR laptops with automatic profile switching.",
    languageLabel: "Interface language",
    navigationLabel: "Main navigation",
    visualLabel: "Charging profile switching demo",
    tabsLabel: "Choose a profile to preview",
    trustLabel: "Key benefits",
    comparisonLabel: "Typical charging compared with battery-friendly charging",
    brandLabel: "Honor Battery Saver — home"
  },
  de: {
    title: "Honor Battery Saver — verlängern Sie die Lebensdauer Ihres HONOR-Akkus",
    description: "Vermeiden Sie unnötig lange volle Akkuladungen: mit automatischen Ladegrenzen von 70 %, 90 % und 100 % für kompatible HONOR-Notebooks. Open Source, ohne Telemetrie.",
    ogDescription: "Akkuschonendes Laden für HONOR-Notebooks mit automatischem Profilwechsel.",
    languageLabel: "Oberflächensprache",
    navigationLabel: "Hauptnavigation",
    visualLabel: "Demo zum Wechseln des Ladeprofils",
    tabsLabel: "Profil für die Vorschau auswählen",
    trustLabel: "Wichtigste Vorteile",
    comparisonLabel: "Normales und akkuschonendes Laden im Vergleich",
    brandLabel: "Honor Battery Saver — Startseite"
  },
  pt: {
    title: "Honor Battery Saver — ajude a bateria do seu HONOR a durar mais",
    description: "Reduza o tempo que a bateria passa em 100% com perfis automáticos de carregamento de 70%, 90% e 100% para notebooks HONOR compatíveis. Código aberto e sem telemetria.",
    ogDescription: "Preserve a bateria do seu notebook HONOR com a troca automática de perfis de carregamento.",
    languageLabel: "Idioma da interface",
    navigationLabel: "Navegação principal",
    visualLabel: "Demonstração da troca de perfis de carregamento",
    tabsLabel: "Escolha um perfil para visualizar",
    trustLabel: "Principais benefícios",
    comparisonLabel: "Comparação entre carregamento comum e carregamento que preserva a bateria",
    brandLabel: "Honor Battery Saver — início"
  },
  ko: {
    title: "Honor Battery Saver — HONOR 배터리 수명을 더 길게",
    description: "호환 HONOR 노트북을 위한 70%, 90%, 100% 자동 충전 프로필로 불필요하게 완충 상태를 유지하는 시간을 줄이세요. 오픈 소스이며 원격 측정 데이터를 수집하지 않습니다.",
    ogDescription: "충전 프로필을 자동으로 전환해 HONOR 노트북의 배터리 부담을 줄여 보세요.",
    languageLabel: "인터페이스 언어",
    navigationLabel: "기본 탐색",
    visualLabel: "충전 프로필 전환 데모",
    tabsLabel: "미리 볼 프로필 선택",
    trustLabel: "주요 이점",
    comparisonLabel: "일반 충전과 배터리 보호 충전 비교",
    brandLabel: "Honor Battery Saver — 홈"
  },
  zh: {
    title: "Honor Battery Saver — 让您的 HONOR 电池更耐用",
    description: "为兼容的 HONOR 笔记本自动切换 70%、90% 和 100% 充电上限，减少电池长时间处于满电状态的情况。开源，不收集遥测数据。",
    ogDescription: "自动切换充电方案，为 HONOR 笔记本电池提供日常养护。",
    languageLabel: "界面语言",
    navigationLabel: "主导航",
    visualLabel: "充电方案切换演示",
    tabsLabel: "选择要预览的方案",
    trustLabel: "主要优势",
    comparisonLabel: "普通充电与电池养护充电对比",
    brandLabel: "Honor Battery Saver — 首页"
  }
};

function detectInitialLanguage() {
  const pageLanguage = document.documentElement.lang.toLowerCase();
  return ["ru", "de", "pt", "ko", "zh"].find((code) => pageLanguage.startsWith(code)) || "en";
}

let language = detectInitialLanguage();
let activeProfile = "home";
let profileAutoplayAnimation;
let pointerActivatedProfileControl = false;

const PROFILE_AUTOPLAY_DELAY = 6000;
const reducedMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
const autoplayPauseReasons = new Set();

const header = document.querySelector(".site-header");
const languageToggle = document.querySelector("[data-language-toggle]");
const languages = [
  ["en", "English"], ["ru", "Русский"], ["de", "Deutsch"],
  ["pt", "Português"], ["ko", "한국어"], ["zh", "简体中文"]
];
const languagePicker = document.createElement("div");
languagePicker.className = "language-picker";

const languageButton = document.createElement("button");
languageButton.className = "language-toggle";
languageButton.type = "button";
languageButton.setAttribute("aria-haspopup", "listbox");
languageButton.setAttribute("aria-expanded", "false");
languageButton.setAttribute("aria-controls", "language-menu");

const languageButtonLabel = document.createElement("span");
languageButtonLabel.className = "language-current";
const languageChevron = document.createElement("span");
languageChevron.className = "language-chevron";
languageChevron.setAttribute("aria-hidden", "true");
languageButton.append(languageButtonLabel, languageChevron);

const languageMenu = document.createElement("div");
languageMenu.className = "language-menu";
languageMenu.id = "language-menu";
languageMenu.role = "listbox";
languageMenu.tabIndex = -1;

const languageOptions = languages.map(([value, label]) => {
  const option = document.createElement("button");
  option.className = "language-option";
  option.type = "button";
  option.role = "option";
  option.dataset.language = value;
  option.textContent = label;
  option.tabIndex = -1;
  languageMenu.append(option);
  return option;
});

languagePicker.append(languageButton, languageMenu);
languageToggle.replaceWith(languagePicker);
const profileButtons = [...document.querySelectorAll("[data-profile]")];
const profileSwitcher = document.querySelector(".profile-switcher");
const metaDescription = document.querySelector('meta[name="description"]');
const ogDescription = document.querySelector('meta[property="og:description"]');
const ogTitle = document.querySelector('meta[property="og:title"]');
const twitterTitle = document.querySelector('meta[name="twitter:title"]');
const twitterDescription = document.querySelector('meta[name="twitter:description"]');
const profileTitle = document.querySelector("[data-profile-title]");
const profileNetwork = document.querySelector("[data-profile-network]");
const profileLimit = document.querySelector("[data-profile-limit]");
const profileStart = document.querySelector("[data-profile-start]");
const profileEnd = document.querySelector("[data-profile-end]");
const batteryChart = document.querySelector("[data-battery-chart]");
const productVisual = document.querySelector(".product-visual");

function stopProfileAutoplay() {
  if (!profileAutoplayAnimation) return;
  profileAutoplayAnimation.onfinish = null;
  profileAutoplayAnimation.cancel();
  profileAutoplayAnimation = undefined;
}

function scheduleProfileAutoplay(restart = false) {
  if (restart) stopProfileAutoplay();
  if (reducedMotionQuery.matches || profileButtons.length < 2) {
    stopProfileAutoplay();
    profileSwitcher.dataset.autoplayState = "disabled";
    return;
  }

  if (!profileAutoplayAnimation) {
    const activeButton = profileButtons.find((button) => button.dataset.profile === activeProfile);
    const progress = activeButton.querySelector(".profile-progress-fill");
    if (!progress || typeof progress.animate !== "function") {
      profileSwitcher.dataset.autoplayState = "disabled";
      return;
    }

    // The bar is also the clock, so pausing it preserves the remaining time.
    profileAutoplayAnimation = progress.animate(
      [{ transform: "scaleX(0)" }, { transform: "scaleX(1)" }],
      { duration: PROFILE_AUTOPLAY_DELAY, easing: "linear", fill: "forwards" }
    );
    profileAutoplayAnimation.pause();
    profileAutoplayAnimation.onfinish = () => {
      const currentIndex = profileButtons.findIndex((button) => button.dataset.profile === activeProfile);
      const nextButton = profileButtons[(currentIndex + 1) % profileButtons.length];
      renderProfile(nextButton.dataset.profile);
      scheduleProfileAutoplay(true);
    };
  }

  const paused = document.hidden || autoplayPauseReasons.size > 0;
  profileSwitcher.dataset.autoplayState = paused ? "paused" : "running";
  if (paused) profileAutoplayAnimation.pause();
  else profileAutoplayAnimation.play();
}

function setAutoplayPaused(reason, paused) {
  if (paused) autoplayPauseReasons.add(reason);
  else autoplayPauseReasons.delete(reason);
  scheduleProfileAutoplay();
}

function renderProfile(name) {
  const profile = profiles[name];
  activeProfile = name;
  profileTitle.textContent = profile.title[language];
  profileNetwork.textContent = profile.network;
  profileLimit.textContent = profile.end;
  profileStart.textContent = `${profile.start}%`;
  profileEnd.textContent = `${profile.end}%`;
  batteryChart.dataset.profileState = name;
  batteryChart.classList.toggle("narrow-range", profile.end - profile.start < 12);

  profileButtons.forEach((button) => {
    const selected = button.dataset.profile === name;
    button.classList.toggle("active", selected);
    button.setAttribute("aria-selected", String(selected));
    button.tabIndex = selected ? 0 : -1;
  });
}

function setLanguage(nextLanguage) {
  language = nextLanguage;
  const content = languageContent[language];
  document.documentElement.lang = language;
  document.title = content.title;
  metaDescription.content = content.description;
  ogDescription.content = content.ogDescription;
  ogTitle.content = content.title;
  twitterTitle.content = content.title;
  twitterDescription.content = content.ogDescription;

  document.querySelectorAll("[data-en]").forEach((element) => {
    const translated = language === "ru"
      ? element.dataset.ru
      : language === "en"
        ? element.dataset.en
        : globalThis.siteTranslations?.[language]?.[element.dataset.en];
    if (translated) element.textContent = translated;
  });

  languageButtonLabel.textContent = languages.find(([code]) => code === language)[1];
  languageButton.setAttribute("aria-label", content.languageLabel);
  languageMenu.setAttribute("aria-label", content.languageLabel);
  languageOptions.forEach((option) => {
    const selected = option.dataset.language === language;
    option.setAttribute("aria-selected", String(selected));
  });
  document.querySelector(".nav").setAttribute("aria-label", content.navigationLabel);
  document.querySelector(".product-visual").setAttribute("aria-label", content.visualLabel);
  document.querySelector(".profile-switcher").setAttribute("aria-label", content.tabsLabel);
  document.querySelector(".trust-row").setAttribute("aria-label", content.trustLabel);
  document.querySelector(".charge-comparison").setAttribute("aria-label", content.comparisonLabel);
  document.querySelector(".brand").setAttribute("aria-label", content.brandLabel);

  renderProfile(activeProfile);
}

function setLanguageMenuOpen(open, focusSelected = false) {
  languagePicker.classList.toggle("open", open);
  languageButton.setAttribute("aria-expanded", String(open));
  if (open && focusSelected) {
    languageOptions.find((option) => option.dataset.language === language)?.focus();
  }
}

languageButton.addEventListener("click", () => {
  setLanguageMenuOpen(!languagePicker.classList.contains("open"), true);
});

languageButton.addEventListener("keydown", (event) => {
  if (!["ArrowDown", "ArrowUp"].includes(event.key)) return;
  event.preventDefault();
  setLanguageMenuOpen(true, true);
});

languageOptions.forEach((option, index) => {
  option.addEventListener("click", () => {
    setLanguageMenuOpen(false);
    if (option.dataset.language === language) {
      languageButton.focus();
      return;
    }
    window.location.href = `/${option.dataset.language}/`;
  });
  option.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      event.preventDefault();
      setLanguageMenuOpen(false);
      languageButton.focus();
      return;
    }
    if (!["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) return;
    event.preventDefault();
    let nextIndex = index;
    if (event.key === "ArrowDown") nextIndex = (index + 1) % languageOptions.length;
    if (event.key === "ArrowUp") nextIndex = (index - 1 + languageOptions.length) % languageOptions.length;
    if (event.key === "Home") nextIndex = 0;
    if (event.key === "End") nextIndex = languageOptions.length - 1;
    languageOptions[nextIndex].focus();
  });
});

document.addEventListener("pointerdown", (event) => {
  if (!languagePicker.contains(event.target)) setLanguageMenuOpen(false);
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && languagePicker.classList.contains("open")) {
    setLanguageMenuOpen(false);
    languageButton.focus();
  }
});

profileButtons.forEach((button, index) => {
  button.addEventListener("pointerdown", () => {
    pointerActivatedProfileControl = true;
  });
  const releasePointerControl = () => {
    window.requestAnimationFrame(() => {
      pointerActivatedProfileControl = false;
    });
  };
  button.addEventListener("pointerup", releasePointerControl);
  button.addEventListener("pointercancel", releasePointerControl);
  button.addEventListener("click", () => {
    renderProfile(button.dataset.profile);
    scheduleProfileAutoplay(true);
  });
  button.addEventListener("keydown", (event) => {
    if (!["ArrowLeft", "ArrowRight"].includes(event.key)) return;
    event.preventDefault();
    const direction = event.key === "ArrowRight" ? 1 : -1;
    const nextIndex = (index + direction + profileButtons.length) % profileButtons.length;
    profileButtons[nextIndex].focus();
    renderProfile(profileButtons[nextIndex].dataset.profile);
    scheduleProfileAutoplay(true);
  });
});

productVisual.addEventListener("mouseenter", () => setAutoplayPaused("pointer", true));
productVisual.addEventListener("mouseleave", () => setAutoplayPaused("pointer", false));
productVisual.addEventListener("focusin", () => {
  if (!pointerActivatedProfileControl) setAutoplayPaused("focus", true);
});
productVisual.addEventListener("focusout", () => {
  window.requestAnimationFrame(() => {
    if (!productVisual.contains(document.activeElement)) setAutoplayPaused("focus", false);
  });
});

document.addEventListener("visibilitychange", () => scheduleProfileAutoplay());
reducedMotionQuery.addEventListener("change", () => scheduleProfileAutoplay());

function updateHeader() {
  header.classList.toggle("scrolled", window.scrollY > 16);
}

window.addEventListener("scroll", updateHeader, { passive: true });
updateHeader();

if ("IntersectionObserver" in window && !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
  document.documentElement.classList.add("motion-ready");
  const observer = new IntersectionObserver((entries) => {
    entries.forEach((entry) => {
      if (!entry.isIntersecting) return;
      entry.target.classList.add("visible");
      observer.unobserve(entry.target);
    });
  }, { threshold: 0.12 });
  document.querySelectorAll(".reveal").forEach((element) => observer.observe(element));
} else {
  document.querySelectorAll(".reveal").forEach((element) => element.classList.add("visible"));
}

setLanguage(language);
scheduleProfileAutoplay();
