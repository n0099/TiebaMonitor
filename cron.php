<?php
require 'core.php';

$time = microtime(true);
$forum = ['模拟城市', 'transportfever', '台风吧官方水', '西奥小镇', '贴吧意见反馈', '贴吧曝光台'];
$curl = curl_init();
curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);

function tieba_magic_time($time) {
    if (preg_match('/^\d{4}-\d{1,2}$/', $time)) {
        return $time . '-01 00:00:00';
    } elseif (preg_match('/^\d{1,2}-\d{1,2}$/', $time)) {
        return date('Y-m-d H:i:s', strtotime(date('Y') - ((time() < strtotime(date('Y') . "-{$time}")) ? 1 : 0) . "-{$time}"));
    } elseif (preg_match('/^\d{1,2}:\d{1,2}$/', $time)) {
        return date('Y-m-d') . " {$time}";
    }
}

foreach ($forum as $tieba) {
    // 贴吧首页
    curl_setopt($curl, CURLOPT_URL, "https://tieba.baidu.com/f?kw={$tieba}&ie=utf-8&pn=0&pagelets=frs-list%2Fpagelet%2Fthread");
    $response = curl_exec($curl);
    // 解码解转义
    preg_match('/<script>Bigpipe.register\("frs-list\/pagelet\/thread_list", (.*),"parent/', $response, $regex_match);
    $explode = explode('<li class=" j_thread_list', htmlspecialchars_decode(json_decode("{$regex_match[1]}}", true)['content']));
    // 话题贴
    preg_match('/<script>Bigpipe.register\("live\/pagelet\/live_thread", (.*),"parent/', $response, $regex_match);
    $topic = json_decode("{$regex_match[1]}}", true);
    if (!empty($topic['content'])) { $explode['topic'] = htmlspecialchars_decode($topic['content']);}
    unset($explode[0]);
    foreach ($explode as $index => $post) {
        if ($index == 'topic') {
            if (strstr($post, '<div class="interview  ">') != false) {
                // 文本话题贴id
                preg_match('/\/\/tieba.baidu.com\/p\/(\d*)/', $post, $regex_match);
                $post_data['id'] = $regex_match[1];
                // 文本话题贴标题
                preg_match('/<a href="\/\/tieba.baidu.com\/p\/\d*" target="_blank" title=".*">\s*(.*)<\/a>/', $post, $regex_match);
                $post_title = trim($regex_match[1]);
                // 文本话题贴发贴人
                preg_match('/<a title="" href="\/\/tieba.baidu.com\/home\/main\?un=.*&ie=utf-8&from=live" target="_blank">\s*(.*)<\/a>/', $post, $regex_match);
                $post_data['author_name'] = trim($regex_match[1]);
                // 文本话题贴回复数
                preg_match('/<span class="listReplyNum inlineBlock" id="interviewReply" title="\d*个回复">(\d*)<\/span>/', $post, $regex_match);
                $post_data['reply_num'] = $regex_match[1];
            } elseif (strstr($post, '<ul id="thread_pic"') != false) {
                // 图片话题贴id
                preg_match('/<li class="clearfix j_thread_list threadlist_li_gray thread_pic_bright"\n\s*id="pic_theme_list"\n\s*tid="(\d*)">/', $post, $regex_match);
                $post_data['id'] = $regex_match[1];
                // curl
                curl_setopt($curl, CURLOPT_URL, "http://tieba.baidu.com/photo/bw/picture/toplist?kw={$tieba}&tid={$post_data['id']}&ie=utf-8");
                $response = curl_exec($curl);
                $pic_topic_data = json_decode($response, true)['data']['thread'];
                // 图片话题贴标题
                $post_title = $pic_topic_data['title'];
                // 图片话题贴发贴人
                $post_data['author_name'] = $pic_topic_data['name'];
                // 图片话题贴回复数
                $post_data['reply_num'] = $pic_topic_data['reply_amount'];
                // 图片话题贴最后回复人
                $post_latest_replyer = $pic_topic_data['last_author'];
                // 图片话题贴最后回复时间
                $post_latest_reply_time = date('Y-m-d H:i', $pic_topic_data['last_time']);
            }
        } else {
            // 主题贴信息
            $post_data = json_decode(strstr(strstr($post, '{"'), '}', true) . '}', true);
            if (empty($post_data['is_good'])) {
                $post_data['is_good'] = 0;
            }
            if (empty($post_data['is_top'])) {
                $post_data['is_top'] = 0;
            }
            // 主题贴标题
            preg_match('/<a rel="noreferrer" href="\/p\/\d*(\?fid=\d*|)" title=".*" target="_blank" class="j_th_tit ">(.*)<\/a>/', $post, $regex_match);
            $post_title = $regex_match[2];
            // 跨吧贴
            $post_is_me0407 = !empty($regex_match[1]);
            // 主题贴发表时间
            preg_match('/<span class="pull-right is_show_create_time" title="创建时间">(.*)<\/span>/', $post, $regex_match);
            $post_time = tieba_magic_time($regex_match[1]);
            // 主题贴最后回复人
            preg_match('/<span class="tb_icon_author_rely j_replyer" title="最后回复人: (.*)">/', $post, $regex_match);
            $post_latest_replyer = $regex_match[1];
            // 主题贴最后回复时间
            preg_match('/<span class="threadlist_reply_date pull_right j_reply_data" title="最后回复时间">\r\n(.*)<\/span>/', $post, $regex_match);
            $post_latest_reply_time = trim($regex_match[1]);
            $post_latest_reply_time = empty($post_latest_reply_time) ? null : tieba_magic_time($post_latest_reply_time);
        }

        $post_sql = $sql->query("SELECT reply_num, latest_replyer, latest_reply_time FROM tbmonitor_post WHERE tid = {$post_data['id']}");
        $post_sql_data = $post_sql == false ? null : $post_sql->fetch_assoc();
        if ($post_sql_data != null) {
            // 避免写入不完整最后回复时间
            $post_latest_reply_time = $post_sql_data['latest_reply_time'] > $post_latest_reply_time ? $post_sql_data['latest_reply_time'] : $post_latest_reply_time;
            // 判断主题贴是否有更新
            $is_post_update = $post_sql_data['reply_num'] != $post_data['reply_num'] || $post_sql_data['latest_replyer'] != $post_latest_replyer || strtotime($post_sql_data['latest_reply_time']) > strtotime($post_latest_reply_time);
        }
        if ($post_sql_data != null && ($index == 'topic' || ($post_sql->num_rows == 0 || ($post_sql->num_rows != 0 && $is_post_update)))) {
            // 获取主题贴第一页回复
            curl_setopt($curl, CURLOPT_URL, "https://tieba.baidu.com/p/{$post_data['id']}?pn=1&ajax=1");
            echo "https://tieba.baidu.com/p/{$post_data['id']}?pn=1&ajax=1\n";
            $response = curl_exec($curl);
            // 获取主题贴回复页数
            preg_match('/共<span class="red">(\d*)<\/span>页/', $response, $regex_match);
            $reply_pages = $regex_match[1];
            // 遍历主题贴所有回复页
            for ($reply_pn = 1; $reply_pn <= $reply_pages; $reply_pn++) {
                $reply_update_log = $sql->query("SELECT id, time FROM tbmonitor_update_log WHERE tid = {$post_data['id']} AND pn = {$reply_pn} AND updated = FALSE ORDER BY id DESC LIMIT 1");
                $reply_update_log = $reply_update_log == false ? false : $reply_update_log->fetch_assoc();
                //$reply_updating = $reply_update_log == false ? false : ($reply_update_log['MAX(time)'] == 'NULL' ? false : true);
                $reply_update_fault = $reply_update_log != false ? strtotime($reply_update_log['time']) < strtotime('-5 mins') : false;
                if ($reply_update_fault) {
                    var_dump($reply_update_fault);
                    $sql->query("UPDATE tbmonitor_update_log SET updated = TRUE WHERE id = \"{$reply_update_log['id']}\"");
                    //$reply_updating = false;
                }
                echo "{$post_data['id']}\n";
                var_dump($reply_update_log);
                var_dump($reply_updating);
                if ($reply_update_log == false) {
                    $reply_update_time = date('Y-m-d H:i:s');
                    $sql->query("INSERT INTO tbmonitor_update_log (time, tid, pn, updated, timing) VALUES (\"{$reply_update_time}\", {$post_data['id']}, {$reply_pn}, FALSE, 0)");
                    $post_updating_log_id = $sql->insert_id;
                    $post_timing = microtime(true);

                    if ($reply_pn != 1) {
                        curl_setopt($curl, CURLOPT_URL, "https://tieba.baidu.com/p/{$post_data['id']}?pn={$reply_pn}&ajax=1");
                        echo "https://tieba.baidu.com/p/{$post_data['id']}?pn={$reply_pn}&ajax=1\n";
                        $response = curl_exec($curl);
                    }
                    $explode = preg_split('/<div class="l_post.*data-field=\'/', $response);
                    foreach ($explode as $reply) {
                        // 回复信息
                        $reply_data = json_decode(htmlspecialchars_decode(strstr($reply, "' >", true)), true);
                        if (empty($reply_data['content']['lzl_num'])) {
                            $reply_data['content']['lzl_num'] = 0;
                        }
                        // 回复内容
                        preg_match('/<cc>(|\s*)<div class="j_ueg_post_content p_forbidden_tip.*?">.*?<\/div><div id="post_content_\d*" class="d_post_content.*?">(.+)<\/div><br>(|\s*)<\/cc>/', $reply, $regex_match);
                        $reply_content = trim($regex_match[2]);
                        // 回复时间
                        if (isset($reply_data['content']['date'])) {
                            $reply_time = $reply_data['content']['date'];
                        } else {
                            preg_match('/<span class="tail-info">(\d{4}-\d{2}-\d{2} \d{2}:\d{2})<\/span>/', $reply, $regex_match);
                            $reply_time = $regex_match[1];
                        }

                        /*$lzl_update_log = $sql->query("SELECT MAX(time) FROM tbmonitor_update_log WHERE tid = {$post_data['id']} AND pid = {$reply_data['content']['post_id']} AND status = \"updating\"");
                        $lzl_update_log = $lzl_update_log == false ? false : $lzl_update_log->fetch_assoc();
                        $lzl_updating = $lzl_update_log == false ? false : ($lzl_update_log['MAX(time)'] == 'NULL' ? false : true);
                        $lzl_update_fault = $lzl_updating == true ? strtotime($lzl_update_log['MAX(time)']) < strtotime('-30 mins') : false;
                        echo "{$reply_data['content']['post_id']}\n";
                        var_dump($lzl_update_log);
                        var_dump($lzl_updating);
                        if ($lzl_update_fault) {
                            $sql->query("UPDATE tbmonitor_update_log SET status = \"updated\" WHERE time = \"{$reply_update_log['MAX(time)']}\"");
                            $lzl_updating = false;
                        }*/
                        $lzl_update_log = false;
                        if ($lzl_update_log == false) {
                            //$lzl_update_time = date('Y-m-d H:i:s');
                            //$sql->query("INSERT INTO tbmonitor_update_log (time, tid, pid, status) VALUES (\"{$lzl_update_time}\", {$post_data['id']}, {$reply_data['content']['post_id']}, \"updating\")");

                            //$reply_update_log = $sql->query("SELECT MAX(time) FROM tbmonitor_update_log WHERE tid = {$post_data['id']} AND status = \"updating\"")->fetch_assoc();
                            //$reply_update_fault = strtotime($reply_update_log['MAX(time)']) < strtotime('-30 mins');
                            // 判断楼中楼是否有更新
                            if ($reply_data['content']['post_no'] == 1 || $reply_data['content']['comment_num'] == 0) {
                                $reply_sql_data = null;
                            } else {
                                echo "SELECT lzl_num FROM tbmonitor_reply WHERE pid = {$reply_data['content']['post_id']}\n";
                                $reply_sql = $sql->query("SELECT lzl_num FROM tbmonitor_reply WHERE pid = {$reply_data['content']['post_id']}");
                                $reply_sql_data = $reply_sql == false ? null : $reply_sql->fetch_assoc();
                            }
                            /*if ($reply_sql_data != null) {
                                if ($reply_data['content']['post_no'] != 1 && $reply_data['content']['comment_num'] != 0) {
                                    if ($reply_sql->num_rows == 0) {
                                        $is_lzl_updated = true;
                                    } elseif ($reply_sql_data['lzl_num'] != $reply_data['content']['comment_num']) {
                                        $is_lzl_updated = true;
                                    }
                                }
                            }// else { $is_lzl_updated = false; }
                            */
                            /*
                            var_dump($post_data['id']);
                            var_dump($is_post_update);
                            var_dump($is_lzl_updated);
                            */
                            //if ($is_lzl_updated) {
                            var_dump($reply_sql_data);
                            if ($reply_sql_data != null && ($reply_sql->num_rows == 0 || ($reply_sql_data['lzl_num'] != $reply_data['content']['comment_num']))) {
                                echo 'gg';
                                curl_setopt($curl, CURLOPT_URL, "https://tieba.baidu.com/p/comment?tid={$post_data['id']}&pid={$reply_data['content']['post_id']}&pn=1");
                                $response = curl_exec($curl);
                                echo "https://tieba.baidu.com/p/comment?tid={$post_data['id']}&pid={$reply_data['content']['post_id']}&pn=1\n";
                                preg_match('/<a href="#(\d*)">尾页<\/a>/', $response, $regex_match);
                                $lzl_pages = empty($regex_match) ? 1 : $regex_match[1];
                                for ($lzl_pn = 1; $lzl_pn <= $lzl_pages; $lzl_pn++) {
                                    if ($lzl_pn != 1) {
                                        curl_setopt($curl, CURLOPT_URL, "https://tieba.baidu.com/p/comment?tid={$post_data['id']}&pid={$reply_data['content']['post_id']}&pn={$lzl_pn}");
                                        echo "https://tieba.baidu.com/p/comment?tid={$post_data['id']}&pid={$reply_data['content']['post_id']}&pn={$lzl_pn}\n";
                                        $response = curl_exec($curl);
                                    }
                                    $explode = explode('<li class="lzl_single_post j_lzl_s_p ', $response);
                                    foreach ($explode as $lzl) {
                                        // 楼中楼信息
                                        preg_match('/data-field=\'({.*?})/', $lzl, $regex_match);
                                        $lzl_date = json_decode(htmlspecialchars_decode($regex_match[1]), true);
                                        // 楼中楼内容
                                        preg_match('/<span class="lzl_content_main">(.*?)<\/span>/', $lzl, $regex_match);
                                        $lzl_content = trim($regex_match[1]);
                                        // 楼中楼回复时间
                                        preg_match('/<span class="lzl_time">(\d{4}-\d{1,2}-\d{1,2} \d{2}:\d{2})<\/span>/', $lzl, $regex_match);
                                        $lzl_reply_time = $regex_match[1];
                                        //var_dump($lzl_date);
                                        // 楼中楼数据库
                                        $lzl_date['user_name'] = $sql->escape_string($lzl_date['user_name']);
                                        $lzl_content = $sql->escape_string($lzl_content);
                                        $query = "INSERT INTO tbmonitor_lzl (forum, tid, pid, spid, author, content, reply_time) VALUES (\"{$tieba}\", {$post_data['id']}, {$reply_data['content']['post_id']}, {$lzl_date['spid']}, \"{$lzl_date['user_name']}\", \"{$lzl_content}\", \"{$lzl_reply_time}\")";
                                        echo $query . "\n";
                                        $sql->query($query);
                                    }
                                }
                            }
                        }
                        // 回复贴数据库
                        $reply_data['author']['user_name'] = $sql->escape_string($reply_data['author']['user_name']);
                        $reply_content = $sql->escape_string($reply_content);
                        $query = "INSERT INTO tbmonitor_reply (forum, tid, pid, author, content, floor, lzl_num, reply_time) VALUES (\"{$tieba}\", {$post_data['id']}, {$reply_data['content']['post_id']}, \"{$reply_data['author']['user_name']}\", \"{$reply_content}\", {$reply_data['content']['post_no']}, {$reply_data['content']['comment_num']}, \"{$reply_time}\") ON DUPLICATE KEY UPDATE lzl_num = {$reply_data['content']['comment_num']}";
                        $sql->query($query);

                        //$sql->query("UPDATE tbmonitor_update_log SET status = \"updated\" WHERE time = \"{$lzl_update_time}\"");
                    }
                }
            }
        }

        // 主题贴数据库
        $post_is_me0407 = empty($post_is_me0407) ? 0 : 1;
        $post_title = $sql->escape_string($post_title);
        $post_data['author_name'] = empty($post_data['author_name']) ? "(SELECT author FROM tbmonitor_reply WHERE pid={$post_data['first_post_id']})" : '"' . $sql->escape_string($post_data['author_name']) . '"';
        $post_latest_replyer = $sql->escape_string($post_latest_replyer);
        $post_latest_reply_time = empty($post_latest_reply_time) ? 'null' : "\"{$post_latest_reply_time}\"";
        if ($index == 'topic') {
            $query = "INSERT INTO tbmonitor_post (forum, tid, title, author, reply_num) VALUES (\"{$tieba}\", {$post_data['id']}, \"{$post_title}\", {$post_data['author_name']}, {$post_data['reply_num']}) ON DUPLICATE KEY UPDATE reply_num={$post_data['reply_num']}";
        } else {
            $query = "INSERT INTO tbmonitor_post (forum, tid, first_post_id, is_top, is_good, is_me0407, title, author, reply_num, post_time, latest_replyer, latest_reply_time) VALUES (\"{$tieba}\", {$post_data['id']}, {$post_data['first_post_id']}, {$post_data['is_top']}, {$post_data['is_good']}, {$post_is_me0407}, \"{$post_title}\", {$post_data['author_name']}, {$post_data['reply_num']}, \"{$post_time}\", \"{$post_latest_replyer}\", {$post_latest_reply_time}) ON DUPLICATE KEY UPDATE author = {$post_data['author_name']}, first_post_id = {$post_data['first_post_id']}, is_top = {$post_data['is_top']}, is_good = {$post_data['is_good']}, reply_num = {$post_data['reply_num']}, post_time = ( SELECT * FROM ( SELECT IFNULL(( SELECT reply_time FROM tbmonitor_reply WHERE pid = {$post_data['first_post_id']} ),( SELECT post_time FROM tbmonitor_post WHERE tid = {$post_data['id']} )) ) AS T ), latest_replyer = \"{$post_latest_replyer}\", latest_reply_time = {$post_latest_reply_time}";
        }
        $sql->query($query);

        $sql->query("UPDATE tbmonitor_update_log SET updated = TRUE, timing = " . round(microtime(true) - $post_timing, 4) . " WHERE id = \"{$post_updating_log_id}\"");
    }
}

// 计时
$sql->query('INSERT INTO tbmonitor_time (end_time, type, time) VALUES ("' . date('Y-m-d H:i:s') . '", "cron", ' . round(microtime(true) - $time, 4) . ')');