const profiles = {
  home: { title: { ru: "Дом", en: "Home" }, network: "HOME_5G", start: 40, end: 70 },
  office: { title: { ru: "Офис", en: "Office" }, network: "STUDIO_WIFI", start: 70, end: 90 },
  travel: { title: { ru: "Поездка", en: "Travel" }, network: "AIRPORT_FREE", start: 95, end: 100 }
};

const languageContent = {
  ru: {
    title: "Honor Battery Saver — помогите HONOR сохранить батарею",
    description: "Сокращайте ненужное время на 100%: Honor Battery Saver автоматически выбирает лимиты 70%, 90% или 100% для ноутбука HONOR. Открытый код, без телеметрии.",
    ogDescription: "Меньше ненужного времени на 100% — больше заботы о ресурсе аккумулятора HONOR.",
    languageLabel: "Switch to English",
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
    ogDescription: "Less unnecessary time at 100% — more care for your HONOR battery.",
    languageLabel: "Переключить на русский",
    navigationLabel: "Main navigation",
    visualLabel: "Charging profile switching demo",
    tabsLabel: "Choose a profile to preview",
    trustLabel: "Key benefits",
    comparisonLabel: "Typical charging compared with battery-friendly charging",
    brandLabel: "Honor Battery Saver — home"
  }
};

const LANGUAGE_STORAGE_KEY = "honor-battery-saver-language";

function detectInitialLanguage() {
  try {
    const savedLanguage = localStorage.getItem(LANGUAGE_STORAGE_KEY);
    if (savedLanguage === "en" || savedLanguage === "ru") return savedLanguage;
  } catch {
    // Language detection still works when browser storage is unavailable.
  }

  const browserLanguages = navigator.languages?.length ? navigator.languages : [navigator.language];
  return browserLanguages.some((value) => value?.toLowerCase().startsWith("ru")) ? "ru" : "en";
}

let language = detectInitialLanguage();
let activeProfile = "home";
let profileAutoplayTimer;
let pointerActivatedProfileControl = false;

const PROFILE_AUTOPLAY_DELAY = 6000;
const reducedMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
const autoplayPauseReasons = new Set();

const header = document.querySelector(".site-header");
const languageToggle = document.querySelector("[data-language-toggle]");
const profileButtons = [...document.querySelectorAll("[data-profile]")];
const metaDescription = document.querySelector('meta[name="description"]');
const ogDescription = document.querySelector('meta[property="og:description"]');
const profileTitle = document.querySelector("[data-profile-title]");
const profileNetwork = document.querySelector("[data-profile-network]");
const profileLimit = document.querySelector("[data-profile-limit]");
const profileStart = document.querySelector("[data-profile-start]");
const profileEnd = document.querySelector("[data-profile-end]");
const batteryChart = document.querySelector("[data-battery-chart]");
const productVisual = document.querySelector(".product-visual");

function stopProfileAutoplay() {
  window.clearTimeout(profileAutoplayTimer);
  profileAutoplayTimer = undefined;
}

function scheduleProfileAutoplay() {
  stopProfileAutoplay();
  if (document.hidden || reducedMotionQuery.matches || autoplayPauseReasons.size > 0) return;

  profileAutoplayTimer = window.setTimeout(() => {
    const currentIndex = profileButtons.findIndex((button) => button.dataset.profile === activeProfile);
    const nextButton = profileButtons[(currentIndex + 1) % profileButtons.length];
    renderProfile(nextButton.dataset.profile);
    scheduleProfileAutoplay();
  }, PROFILE_AUTOPLAY_DELAY);
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

function setLanguage(nextLanguage, persist = false) {
  language = nextLanguage;
  const content = languageContent[language];
  document.documentElement.lang = language;
  document.title = content.title;
  metaDescription.content = content.description;
  ogDescription.content = content.ogDescription;

  document.querySelectorAll("[data-ru][data-en]").forEach((element) => {
    element.textContent = element.dataset[language];
  });

  languageToggle.innerHTML = language === "en"
    ? '<span class="lang-active">EN</span><span aria-hidden="true">/</span><span>RU</span>'
    : '<span>EN</span><span aria-hidden="true">/</span><span class="lang-active">RU</span>';
  languageToggle.setAttribute("aria-label", content.languageLabel);
  document.querySelector(".nav").setAttribute("aria-label", content.navigationLabel);
  document.querySelector(".product-visual").setAttribute("aria-label", content.visualLabel);
  document.querySelector(".profile-switcher").setAttribute("aria-label", content.tabsLabel);
  document.querySelector(".trust-row").setAttribute("aria-label", content.trustLabel);
  document.querySelector(".charge-comparison").setAttribute("aria-label", content.comparisonLabel);
  document.querySelector(".brand").setAttribute("aria-label", content.brandLabel);

  if (persist) {
    try {
      localStorage.setItem(LANGUAGE_STORAGE_KEY, language);
    } catch {
      // The selected language remains active for this page when storage is unavailable.
    }
  }

  renderProfile(activeProfile);
}

languageToggle.addEventListener("click", () => setLanguage(language === "ru" ? "en" : "ru", true));

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
    scheduleProfileAutoplay();
  });
  button.addEventListener("keydown", (event) => {
    if (!["ArrowLeft", "ArrowRight"].includes(event.key)) return;
    event.preventDefault();
    const direction = event.key === "ArrowRight" ? 1 : -1;
    const nextIndex = (index + direction + profileButtons.length) % profileButtons.length;
    profileButtons[nextIndex].focus();
    renderProfile(profileButtons[nextIndex].dataset.profile);
    scheduleProfileAutoplay();
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

document.addEventListener("visibilitychange", scheduleProfileAutoplay);
reducedMotionQuery.addEventListener("change", scheduleProfileAutoplay);

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
