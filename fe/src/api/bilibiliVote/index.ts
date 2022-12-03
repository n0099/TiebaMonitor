import allCandidatesVoteCount from './allCandidatesVoteCount.json';
import allVotesCountGroupByHour from './allVotesCountGroupByHour.json';
import allVotesCountGroupByMinute from './allVotesCountGroupByMinute.json';
import candidateNames from './candidateNames.json';
import top5CandidatesVoteCountGroupByHour from './top5CandidatesVoteCountGroupByHour.json';
import top5CandidatesVoteCountGroupByMinute from './top5CandidatesVoteCountGroupByMinute.json';
import top10CandidatesTimeline from './top10CandidatesTimeline.json';
import top50CandidatesVoteCount from './top50CandidatesVoteCount.json';
import top50CandidatesOfficialValidVoteCount from './top50CandidatesOfficialValidVoteCount.json';
import type { BoolInt, Float, SqlDateTimeUtcPlus8, UInt, UnixTimestamp } from '@/shared';

export const json: {
    allCandidatesVoteCount: AllCandidatesVoteCount,
    allVotesCountGroupByHour: AllVoteCountsGroupByTime,
    allVotesCountGroupByMinute: AllVoteCountsGroupByTime,
    candidateNames: CandidatesName,
    top5CandidatesVoteCountGroupByHour: Top5CandidatesVoteCountGroupByTime,
    top5CandidatesVoteCountGroupByMinute: Top5CandidatesVoteCountGroupByTime,
    top10CandidatesTimeline: Top10CandidatesTimeline,
    top50CandidatesVoteCount: Top50CandidatesVoteCount,
    top50CandidatesOfficialValidVoteCount: Top50CandidatesOfficialValidVoteCount
} = {
    allCandidatesVoteCount: allCandidatesVoteCount as AllCandidatesVoteCount,
    allVotesCountGroupByHour: allVotesCountGroupByHour as AllVoteCountsGroupByTime,
    allVotesCountGroupByMinute: allVotesCountGroupByMinute as AllVoteCountsGroupByTime,
    candidateNames,
    top5CandidatesVoteCountGroupByHour: top5CandidatesVoteCountGroupByHour as Top5CandidatesVoteCountGroupByTime,
    top5CandidatesVoteCountGroupByMinute: top5CandidatesVoteCountGroupByMinute as Top5CandidatesVoteCountGroupByTime,
    top10CandidatesTimeline: top10CandidatesTimeline as Top10CandidatesTimeline,
    top50CandidatesVoteCount: top50CandidatesVoteCount as Top50CandidatesVoteCount,
    top50CandidatesOfficialValidVoteCount
};

export type IsValid = BoolInt;
export type GroupByTimeGranularity = 'hour' | 'minute';
export type CandidatesName = string[];
export type AllCandidatesVoteCount = Array<{
    isValid: IsValid,
    voteFor: UInt,
    count: UInt
}>;
export type Top50CandidatesOfficialValidVoteCount = Array<{
    voteFor: UInt,
    officialValidCount: UInt
}>;
export type Top50CandidatesVoteCount = AllCandidatesVoteCount & Array<{ voterAvgGrade: Float }>;
export type Top5CandidatesVoteCountGroupByTime = AllCandidatesVoteCount & Array<{ time: SqlDateTimeUtcPlus8 }>;
export type AllVoteCountsGroupByTime = Array<{
    time: SqlDateTimeUtcPlus8,
    isValid: IsValid,
    count: UInt
}>;
export type Top10CandidatesTimeline = AllCandidatesVoteCount & Array<{ endTime: UnixTimestamp }>;
