// Detail page: ensure images inside ck-content don't have inline w/h
window.addEventListener('DOMContentLoaded', () => {
  document.querySelectorAll('.course-content img').forEach(img => {
    img.removeAttribute('width');
    img.removeAttribute('height');
  });

  const relatedTrack = document.getElementById('relatedCourseTrack');
  const relatedButtons = document.querySelectorAll('[data-related-direction]');

  if (relatedTrack && relatedButtons.length) {
    const updateRelatedButtons = () => {
      const maxScrollLeft = relatedTrack.scrollWidth - relatedTrack.clientWidth;

      relatedButtons.forEach(button => {
        const direction = Number(button.dataset.relatedDirection);
        const isDisabled = direction < 0
          ? relatedTrack.scrollLeft <= 2
          : relatedTrack.scrollLeft >= maxScrollLeft - 2;

        button.disabled = isDisabled;
      });
    };

    relatedButtons.forEach(button => {
      button.addEventListener('click', () => {
        const direction = Number(button.dataset.relatedDirection);
        const card = relatedTrack.querySelector('.related-course-card');
        const gap = 20;
        const distance = card ? card.getBoundingClientRect().width + gap : relatedTrack.clientWidth * 0.8;

        relatedTrack.scrollBy({ left: direction * distance, behavior: 'smooth' });
      });
    });

    relatedTrack.addEventListener('scroll', updateRelatedButtons, { passive: true });
    window.addEventListener('resize', updateRelatedButtons);
    updateRelatedButtons();
  }
});
