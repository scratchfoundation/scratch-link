/*
 * This field turns each fieldset legend into a button which collapses and expands the containing fieldset.
 */

window.addEventListener('load', function () {
    document.querySelectorAll('fieldset > legend').forEach(function (legendElement) {
        function toggleCollapseFieldSet(mouseEvent) {
            const legend = mouseEvent.currentTarget;
            const fieldset = legend.parentElement;
            if (fieldset.style.height) {
                fieldset.style.overflow = null;
                fieldset.style.height = null; // expand
            } else {
                fieldset.style.overflow = 'scroll';
                fieldset.style.height = '3em'; // collapse
            }
        }
        legendElement.addEventListener('click', toggleCollapseFieldSet);
    });
});
