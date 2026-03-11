(() => {
  let overlay = document.getElementById('lightbox-overlay');
  if (!overlay) {
    overlay = document.createElement('div');
    overlay.id = 'lightbox-overlay';
    const img = document.createElement('img');
    img.id = 'lightbox-full';
    overlay.appendChild(img);
    document.body.appendChild(overlay);

    overlay.addEventListener('click', () => {
      overlay.style.display = 'none';
    });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') overlay.style.display = 'none';
    });
  }

  document.querySelectorAll('.sl-markdown-content img').forEach((img) => {
    img.addEventListener('click', () => {
      document.getElementById('lightbox-full').src = img.src;
      overlay.style.display = 'flex';
    });
  });
})();
