// eslint-disable-next-line import-x/no-unresolved
import viewer from 'v-viewer';
import 'viewerjs/dist/viewer.css';

export const useViewerStore = defineStore('viewer', () => {
    const isEnabled = ref(false);
    const enable = () => {
        if (isEnabled.value)
            return;

        useNuxtApp().vueApp.use(viewer, {
            defaultOptions: {
                url: 'data-origin',
                filter: (img: HTMLImageElement) => img.classList.contains('tieba-ugc-image')
            }
        });
        isEnabled.value = true;
    };

    // returning ref isEnabled while SSR will be store its value in hydration
    // leads to only server, but not including client have enabled the plugin
    // eslint-disable-next-line pinia/require-setup-store-properties-export
    return { enable };
});
