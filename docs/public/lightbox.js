// lightbox.js — Click-to-zoom overlay for markdown images.
// Injected via astro.config.mjs as a deferred script tag.
// Uses `astro:page-load` to re-initialize after Starlight view transitions.

function initImages() {
  // Create the overlay element once (persists across navigations)
  let overlay = document.getElementById('lightbox-overlay');
  if (!overlay) {
    overlay = document.createElement('div');
    overlay.id = 'lightbox-overlay';
    const img = document.createElement('img');
    img.id = 'lightbox-full';
    overlay.appendChild(img);
    document.body.appendChild(overlay);

    // Close on click or Escape
    overlay.addEventListener('click', () => {
      overlay.style.display = 'none';
    });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') overlay.style.display = 'none';
    });
  }

  // Attach click handlers to any new (unprocessed) markdown images.
  // The data-lightbox attribute prevents double-binding after navigations.
  document.querySelectorAll('.sl-markdown-content img:not([data-lightbox])').forEach((img) => {
    img.setAttribute('data-lightbox', '');
    img.addEventListener('click', () => {
      document.getElementById('lightbox-full').src = img.src;
      overlay.style.display = 'flex';
    });
  });
}

// Run on initial page load
initImages();

// Re-run after each Starlight view transition (client-side navigation)
document.addEventListener('astro:page-load', initImages);
