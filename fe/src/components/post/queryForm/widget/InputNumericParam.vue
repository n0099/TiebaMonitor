<template>
<input
    v-if="modelValue.subParam.range === 'IN'"
    @input="emitModelChange($event)" :value="modelValue.value"
    :placeholder="placeholders.IN" :aria-label="modelValue.name"
    type="text" class="form-control" required pattern="\d+(,\d+)+" />
<input
    v-else-if="modelValue.subParam.range === 'BETWEEN'"
    @input="emitModelChange($event)" :value="modelValue.value"
    :placeholder="placeholders.BETWEEN" :aria-label="modelValue.name"
    type="text" class="col-3 form-control flex-grow-0" required pattern="\d+,\d+" />
<input
    v-else @input="emitModelChange($event)" :value="modelValue.value"
    :placeholder="placeholders.equals" :aria-label="modelValue.name"
    type="number" class="col-2 form-control flex-grow-0" required />
</template>

<script setup lang="ts">
import _ from 'lodash';

defineProps<{ placeholders: Record<'BETWEEN' | 'IN' | 'equals', string> }>();
// eslint-disable-next-line vue/define-emits-declaration
defineEmits({
    'update:modelValue': (p: KnownNumericParams) =>
        _.isString(p.name) && _.isString(p.value)
        && numericParamSubParamRangeValues.includes(p.subParam.range)
});
const modelValue = defineModel<KnownNumericParams>({ required: true });

const emitModelChange = (e: Event) => {
    modelValue.value = { ...modelValue.value, value: (e.target as HTMLInputElement).value };
};
</script>

<style scoped>
.col-2 {
    inline-size: 16% !important;
}
.col-3 {
    inline-size: 25% !important;
}
</style>
