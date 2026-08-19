const API='http://127.0.0.1:5177/api';
const l=await fetch(`${API}/auth/login`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({mobile:'09120000011',password:'E2E-Admin-Only-aA1!'})});
const t=(await l.json()).data.accessToken;
const r=await fetch(`${API}/admin/blog`,{method:'POST',headers:{'Content-Type':'application/json',Authorization:`Bearer ${t}`},body:JSON.stringify({title:'مطلب آزمایشی سوییپ',slug:'sweep-blog-post',summary:'خلاصه',contentHtml:'<h2>سرفصل</h2><p>متن کامل مطلب برای سوییپ.</p>',isPublished:true})});
console.log('seed blog post:', r.status);
