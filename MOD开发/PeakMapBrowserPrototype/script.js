const uploadOpen = document.querySelector("#uploadOpen");
const uploadClose = document.querySelector("#uploadClose");
const uploadModal = document.querySelector("#uploadModal");
const cards = document.querySelectorAll(".map-card");

uploadOpen.addEventListener("click", () => {
  uploadModal.classList.add("open");
  uploadModal.setAttribute("aria-hidden", "false");
});

uploadClose.addEventListener("click", () => {
  uploadModal.classList.remove("open");
  uploadModal.setAttribute("aria-hidden", "true");
});

uploadModal.addEventListener("click", (event) => {
  if (event.target === uploadModal) {
    uploadModal.classList.remove("open");
    uploadModal.setAttribute("aria-hidden", "true");
  }
});

cards.forEach((card) => {
  card.addEventListener("click", () => {
    cards.forEach((item) => item.classList.remove("selected"));
    card.classList.add("selected");
  });
});
