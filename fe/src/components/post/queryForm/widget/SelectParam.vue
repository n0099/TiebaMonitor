<template>
<select
    @change="$emit('paramChange', ($event.target as HTMLSelectElement).value)"
    :value="currentParam" class="form-select form-control flex-grow-0">
    <option :disabled="currentParam !== 'add'" :hidden="currentParam !== 'add'" value="add">New...</option>
    <optgroup v-for="(group, groupName) in paramsGroup" :key="groupName" :label="groupName">
        <option
            v-for="(paramDescription, paramName) in group" :key="paramName"
            :value="paramName" :selected="paramName === currentParam">
            {{ paramDescription }}
        </option>
    </optgroup>
</select>
</template>

<script lang="ts">
const paramsGroup = {
    帖子ID: {
        tid: 'tid（主题帖ID）',
        pid: 'pid（回复帖ID）',
        spid: 'spid（楼中楼ID）'
    },
    所有帖子类型: {
        postedAt: '发帖时间',
        authorUid: '发帖人百度UID',
        authorName: '发帖人用户名',
        authorDisplayName: '发帖人覆盖名',
        authorGender: '发帖人性别',
        authorManagerType: '发帖人吧务级别'
    },
    仅主题帖: {
        latestReplyPostedAt: '最后回复时间',
        threadTitle: '主题帖标题',
        threadViewCount: '主题帖浏览量',
        threadShareCount: '主题帖分享量',
        threadReplyCount: '主题帖回复量',
        threadProperties: '主题帖属性',
        latestReplierUid: '最后回复人百度UID',
        latestReplierName: '最后回复人用户名',
        latestReplierDisplayName: '最后回复人覆盖名',
        latestReplierGender: '最后回复人性别'
    },
    仅回复帖: {
        replySubReplyCount: '楼中楼回复量'
    },
    仅回复帖或楼中楼: {
        postContent: '帖子内容',
        authorExpGrade: '发帖人经验等级'
    }
};
</script>

<script setup lang="ts">
import _ from 'lodash';

defineProps<{ currentParam: string }>();
// eslint-disable-next-line vue/define-emits-declaration
defineEmits({
    paramChange: (p: string) => _.includes(_.flatMap(paramsGroup, Object.keys), p)
});
</script>

<style scoped>
select {
    inline-size: 20% !important;
}
</style>
